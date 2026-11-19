using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Commands.UpdateInClassShift;

public sealed record UpdateInClassShiftCommand(
    Guid ActorUserId, Guid ShiftId, string ShiftName, DateTime StartTime, DateTime EndTime,
    string? Description = null, string QuestionPoolMode = "ALL_COURSE_BANK",
    IReadOnlyList<Guid>? SelectedQuestionIds = null, int TotalQuestionsPerStudent = 1,
    bool HasFollowUp = false, int MaxFollowUpQuestions = 2, bool IsAutoPublishGrades = true,
    int ExamDurationMinutes = 20, IReadOnlyList<string>? SelectedDifficulties = null,
    IReadOnlyList<Guid>? SelectedCloIds = null) : IRequest<Result>;

public sealed class UpdateInClassShiftCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateInClassShiftCommand, Result>
{
    public Task<Result> Handle(UpdateInClassShiftCommand request, CancellationToken cancellationToken) =>
        context.ExecuteShiftMutationAsync(request.ShiftId, false, async ct =>
        {
            var shift = await context.InClassExamShifts.Include(x => x.Class).ThenInclude(x => x.Course).ThenInclude(x => x.AcademicProgram)
                .Include(x => x.Class).ThenInclude(x => x.CourseOffering)
                .FirstOrDefaultAsync(x => x.Id == request.ShiftId, ct);
            if (shift is null) return Result.Failure(InClassShiftErrors.ShiftNotFound);
            var error = await InClassShiftMutationGuard.AuthorizeAsync(context, request.ActorUserId, shift, ct)
                ?? await InClassShiftMutationGuard.CheckMutableAsync(context, shift, ct);
            if (error is not null) return Result.Failure(error);
            if (!InClassShiftAcademicContext.IsOperational(shift.Class)) return Result.Failure(InClassShiftErrors.ClassNotFound);
            if (string.IsNullOrWhiteSpace(request.ShiftName) || request.ShiftName.Trim().Length > 255)
                return Result.Failure("Tên ca thi phải có từ 1 đến 255 ký tự.");
            var start = ToUtc(request.StartTime);
            var end = ToUtc(request.EndTime);
            if (start <= DateTime.UtcNow) return Result.Failure("Giờ bắt đầu ca thi mới phải ở tương lai.");
            if (end <= start) return Result.Failure(InClassShiftErrors.InvalidTimeRange);
            if (request.ExamDurationMinutes is < 1 or > 60) return Result.Failure("Thời gian làm bài phải từ 1 đến 60 phút.");
            if (end - start < TimeSpan.FromMinutes(request.ExamDurationMinutes))
                return Result.Failure("Khung giờ ca thi phải đủ thời gian làm bài đã cấu hình.");
            if (request.TotalQuestionsPerStudent is < 1 or > 10) return Result.Failure("Số câu hỏi mỗi sinh viên phải từ 1 đến 10.");
            if (request.MaxFollowUpQuestions is < 0 or > 5) return Result.Failure("Số câu hỏi phụ tối đa phải từ 0 đến 5.");

            var mode = string.IsNullOrWhiteSpace(request.QuestionPoolMode)
                ? InClassShiftPoolConfig.AllCourseBank
                : request.QuestionPoolMode.Trim().ToUpperInvariant();

            if (mode is not (InClassShiftPoolConfig.AllCourseBank or InClassShiftPoolConfig.SelectedClos or InClassShiftPoolConfig.SelectedPoolLegacy))
                return Result.Failure(InClassShiftErrors.InvalidPoolMode);

            if (!InClassShiftPoolConfig.TryNormalizeCreateDifficulties(
                    request.SelectedDifficulties,
                    out var selectedDifficulties,
                    out var wasExplicitDifficulties,
                    out var difficultyError))
            {
                return Result.Failure(difficultyError!);
            }

            var ids = request.SelectedQuestionIds?.Distinct().ToList() ?? [];
            var orderedCloIds = new List<Guid>();

            if (mode == InClassShiftPoolConfig.SelectedPoolLegacy)
            {
                if (ids.Contains(Guid.Empty) || ids.Count < request.TotalQuestionsPerStudent)
                    return Result.Failure("Danh sách câu hỏi được chọn phải đủ số câu mỗi sinh viên và không chứa ID rỗng.");
                var legacyQuestions = context.PracticeQuestions.AsNoTracking()
                    .Where(x => x.CourseId == shift.Class.CourseId && x.IsActive && x.ApprovalStatus == ApprovalStatus.Approved);
                if (await legacyQuestions.CountAsync(x => ids.Contains(x.Id), ct) != ids.Count)
                    return Result.Failure(InClassShiftErrors.InvalidCourseQuestion);
            }
            else
            {
                if (ids.Any(id => id != Guid.Empty))
                    return Result.Failure(InClassShiftErrors.LegacySelectedQuestionIdsForbidden);
                ids.Clear();

                var rawApproved = await context.PracticeQuestions
                    .AsNoTracking()
                    .Where(q => q.CourseId == shift.Class.CourseId && q.IsActive && q.ApprovalStatus == ApprovalStatus.Approved)
                    .Select(q => new { q.Id, q.CloId, q.Difficulty })
                    .ToListAsync(ct);

                var normalizedApproved = new List<PoolCandidateQuestion>(rawApproved.Count);
                foreach (var q in rawApproved)
                {
                    if (InClassShiftPoolConfig.TryNormalizeDifficulty(q.Difficulty, out var normDiff))
                        normalizedApproved.Add(new PoolCandidateQuestion(q.Id, q.CloId, normDiff));
                }

                if (mode == InClassShiftPoolConfig.AllCourseBank)
                {
                    var countByDiff = InClassShiftPoolConfig.CanonicalDifficulties
                        .ToDictionary(d => d, d => normalizedApproved.Count(q => q.Difficulty == d), StringComparer.Ordinal);

                    if (wasExplicitDifficulties)
                    {
                        var zeroDiffs = selectedDifficulties.Where(d => countByDiff.GetValueOrDefault(d, 0) <= 0).ToList();
                        if (zeroDiffs.Count > 0)
                        {
                            var missingLabels = string.Join(", ", zeroDiffs.Select(InClassShiftPoolConfig.GetDifficultyLabel));
                            return Result.Failure($"Thiếu câu hỏi hợp lệ ở mức {missingLabels} (0 câu đã duyệt trong ngân hàng môn). Không tự bù bằng mức ngoài lựa chọn.");
                        }
                    }

                    var totalAvailable = selectedDifficulties.Sum(d => countByDiff.GetValueOrDefault(d, 0));
                    if (totalAvailable < request.TotalQuestionsPerStudent)
                    {
                        return Result.Failure(InClassShiftErrors.InsufficientQuestionPool);
                    }
                }
                else
                {
                    var requestedCloIds = (request.SelectedCloIds ?? Array.Empty<Guid>())
                        .Where(id => id != Guid.Empty)
                        .Distinct()
                        .ToList();

                    if (requestedCloIds.Count == 0)
                        return Result.Failure(InClassShiftErrors.EmptySelectedClos);

                    var matchedClos = await context.CourseClos
                        .AsNoTracking()
                        .Where(c => c.CourseId == shift.Class.CourseId && c.IsActive && requestedCloIds.Contains(c.Id))
                        .OrderBy(c => c.OrderIndex)
                        .ThenBy(c => c.CloCode)
                        .Select(c => new { c.Id, c.CloCode })
                        .ToListAsync(ct);

                    if (matchedClos.Count != requestedCloIds.Count)
                        return Result.Failure(InClassShiftErrors.InvalidCourseClo);

                    orderedCloIds = matchedClos.Select(c => c.Id).ToList();

                    var cloSet = orderedCloIds.ToHashSet();
                    var diffSet = selectedDifficulties.ToHashSet(StringComparer.Ordinal);
                    var cloScopedQuestions = normalizedApproved
                        .Where(q => q.CloId.HasValue && cloSet.Contains(q.CloId.Value) && diffSet.Contains(q.Difficulty))
                        .ToList();

                    var emptyCloCodes = matchedClos
                        .Where(c => cloScopedQuestions.All(q => q.CloId != c.Id))
                        .Select(c => c.CloCode)
                        .ToList();

                    if (emptyCloCodes.Count > 0)
                    {
                        var diffLabels = string.Join(", ", selectedDifficulties.Select(InClassShiftPoolConfig.GetDifficultyLabel));
                        return Result.Failure($"Thiếu câu hỏi hợp lệ ở {string.Join(", ", emptyCloCodes)} cho mức {diffLabels} (0 câu). Hệ thống không tự bù bằng câu ngoài CLO/mức đã chọn.");
                    }

                    if (wasExplicitDifficulties)
                    {
                        var zeroDiffsInClos = selectedDifficulties.Where(d => cloScopedQuestions.All(q => q.Difficulty != d)).ToList();
                        if (zeroDiffsInClos.Count > 0)
                        {
                            var missingDiffLabels = string.Join(", ", zeroDiffsInClos.Select(InClassShiftPoolConfig.GetDifficultyLabel));
                            return Result.Failure($"Các CLO đã chọn chưa có câu hỏi hợp lệ ở mức {missingDiffLabels} (0 câu).");
                        }
                    }

                    if (cloScopedQuestions.Count < request.TotalQuestionsPerStudent)
                    {
                        var perCloSummary = string.Join(", ", matchedClos.Select(c => $"{c.CloCode}: {cloScopedQuestions.Count(q => q.CloId == c.Id)} câu"));
                        return Result.Failure($"Tổng câu hợp lệ của các CLO đã chọn không đủ: cần {request.TotalQuestionsPerStudent} câu mỗi sinh viên nhưng chỉ có {cloScopedQuestions.Count} câu ({perCloSummary}).");
                    }
                }
            }

            shift.ShiftName = request.ShiftName.Trim();
            shift.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            shift.StartTime = start;
            shift.EndTime = end;
            shift.QuestionPoolMode = mode;
            shift.SelectedQuestionIdsJson = JsonSerializer.Serialize(ids);
            shift.SelectedDifficultiesJson = InClassShiftPoolConfig.SerializeDifficulties(selectedDifficulties);
            shift.SelectedCloIdsJson = InClassShiftPoolConfig.SerializeGuidList(orderedCloIds);
            shift.TotalQuestionsPerStudent = request.TotalQuestionsPerStudent;
            shift.HasFollowUp = request.HasFollowUp;
            shift.MaxFollowUpQuestions = request.HasFollowUp ? (request.MaxFollowUpQuestions == 0 ? 2 : request.MaxFollowUpQuestions) : 0;
            shift.IsAutoPublishGrades = request.IsAutoPublishGrades;
            shift.ExamDurationMinutes = request.ExamDurationMinutes;
            shift.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);

    private static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
