using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Common.Security;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Commands.CreateInClassShift;

public sealed class CreateInClassShiftCommandHandler(
    IApplicationDbContext context,
    IValidator<CreateInClassShiftCommand> validator)
    : IRequestHandler<CreateInClassShiftCommand, Result<InClassExamShiftResponse>>
{
    public async Task<Result<InClassExamShiftResponse>> Handle(
        CreateInClassShiftCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<InClassExamShiftResponse>.Failure(validationResult.Errors[0].ErrorMessage);
        }

        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive || actor.Role != UserRole.Lecturer)
        {
            return Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var classEntity = await context.Classes
            .Include(c => c.Course)
            .Include(c => c.CourseOffering)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (!InClassShiftAcademicContext.IsOperational(classEntity))
        {
            return Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.ClassNotFound);
        }

        // Nếu là giảng viên thông thường, kiểm tra xem có được phân công phụ trách lớp này không
        if (actor.Role == UserRole.Lecturer)
        {
            bool isAssigned = classEntity.LecturerId == actor.Id;

            if (!isAssigned)
            {
                return Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
            }
        }

        var normalizedPoolMode = string.IsNullOrWhiteSpace(request.QuestionPoolMode)
            ? InClassShiftPoolConfig.AllCourseBank
            : request.QuestionPoolMode.Trim().ToUpperInvariant();

        if (normalizedPoolMode is not (InClassShiftPoolConfig.AllCourseBank or InClassShiftPoolConfig.SelectedClos))
        {
            return Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.InvalidPoolMode);
        }

        if (request.SelectedQuestionIds is not null && request.SelectedQuestionIds.Any(id => id != Guid.Empty))
        {
            return Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.LegacySelectedQuestionIdsForbidden);
        }

        if (!InClassShiftPoolConfig.TryNormalizeCreateDifficulties(
                request.SelectedDifficulties,
                out var selectedDifficulties,
                out var wasExplicitDifficulties,
                out var difficultyError))
        {
            return Result<InClassExamShiftResponse>.Failure(difficultyError!);
        }

        string secretKey = TotpPasswordHelper.GenerateSecretKey(32);
        int maxFollowUp = request.HasFollowUp ? Math.Clamp(request.MaxFollowUpQuestions <= 0 ? 2 : request.MaxFollowUpQuestions, 1, 5) : 0;

        var startUtc = ToUtc(request.StartTime);
        var endUtc = ToUtc(request.EndTime);
        var nowUtc = DateTime.UtcNow;

        if (endUtc <= startUtc)
        {
            return Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.InvalidTimeRange);
        }

        if (endUtc <= nowUtc)
        {
            return Result<InClassExamShiftResponse>.Failure(
                "Giờ đóng ca đã qua. Hãy chọn khung giờ còn ở tương lai trước khi tạo ca.");
        }

        var examMinutes = Math.Clamp(request.ExamDurationMinutes, 1, 60);
        if (endUtc - startUtc < TimeSpan.FromMinutes(examMinutes))
        {
            return Result<InClassExamShiftResponse>.Failure(
                $"Khung giờ ca phải dài hơn hoặc bằng thời gian làm bài ({examMinutes} phút).");
        }

        var rawApprovedQuestions = await context.PracticeQuestions
            .AsNoTracking()
            .Where(q =>
                q.CourseId == classEntity.CourseId &&
                q.ApprovalStatus == ApprovalStatus.Approved &&
                q.IsActive)
            .Select(q => new
            {
                q.Id,
                q.CloId,
                q.Difficulty
            })
            .ToListAsync(cancellationToken);

        var normalizedApprovedQuestions = new List<PoolCandidateQuestion>(rawApprovedQuestions.Count);
        foreach (var q in rawApprovedQuestions)
        {
            if (InClassShiftPoolConfig.TryNormalizeDifficulty(q.Difficulty, out var normDiff))
            {
                normalizedApprovedQuestions.Add(new PoolCandidateQuestion(q.Id, q.CloId, normDiff));
            }
        }

        var orderedCloIds = new List<Guid>();
        var orderedCloCodes = new List<string>();

        if (normalizedPoolMode == InClassShiftPoolConfig.AllCourseBank)
        {
            var countByDiff = InClassShiftPoolConfig.CanonicalDifficulties
                .ToDictionary(
                    d => d,
                    d => normalizedApprovedQuestions.Count(q => q.Difficulty == d),
                    StringComparer.Ordinal);

            if (wasExplicitDifficulties)
            {
                var zeroDiffs = selectedDifficulties
                    .Where(d => countByDiff.GetValueOrDefault(d, 0) <= 0)
                    .ToList();

                if (zeroDiffs.Count > 0)
                {
                    var missingLabels = string.Join(", ", zeroDiffs.Select(InClassShiftPoolConfig.GetDifficultyLabel));
                    return Result<InClassExamShiftResponse>.Failure(
                        $"Thiếu câu hỏi hợp lệ ở mức {missingLabels} (0 câu đã duyệt trong ngân hàng môn). Không tự bù bằng mức ngoài lựa chọn.");
                }
            }

            var totalAvailable = selectedDifficulties.Sum(d => countByDiff.GetValueOrDefault(d, 0));
            if (totalAvailable < request.TotalQuestionsPerStudent)
            {
                var breakdown = string.Join(
                    ", ",
                    selectedDifficulties.Select(d => $"{InClassShiftPoolConfig.GetDifficultyLabel(d)}: {countByDiff.GetValueOrDefault(d, 0)}"));
                return Result<InClassExamShiftResponse>.Failure(
                    $"Ngân hàng môn không đủ câu hỏi hợp lệ: cần {request.TotalQuestionsPerStudent} câu mỗi sinh viên nhưng chỉ có {totalAvailable} câu ({breakdown}).");
            }
        }
        else
        {
            var requestedCloIds = (request.SelectedCloIds ?? Array.Empty<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (requestedCloIds.Count == 0)
            {
                return Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.EmptySelectedClos);
            }

            var matchedClos = await context.CourseClos
                .AsNoTracking()
                .Where(c =>
                    c.CourseId == classEntity.CourseId &&
                    c.IsActive &&
                    requestedCloIds.Contains(c.Id))
                .OrderBy(c => c.OrderIndex)
                .ThenBy(c => c.CloCode)
                .Select(c => new
                {
                    c.Id,
                    c.CloCode
                })
                .ToListAsync(cancellationToken);

            if (matchedClos.Count != requestedCloIds.Count)
            {
                return Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.InvalidCourseClo);
            }

            orderedCloIds = matchedClos.Select(c => c.Id).ToList();
            orderedCloCodes = matchedClos.Select(c => c.CloCode).ToList();

            if (orderedCloIds.Count > request.TotalQuestionsPerStudent)
            {
                return Result<InClassExamShiftResponse>.Failure(
                    $"Số CLO đã chọn ({orderedCloIds.Count}) lớn hơn số câu mỗi sinh viên ({request.TotalQuestionsPerStudent}). Hãy giảm số CLO hoặc tăng số câu mỗi sinh viên để mỗi sinh viên nhận ít nhất 1 câu từ mỗi CLO.");
            }

            var cloSet = orderedCloIds.ToHashSet();
            var diffSet = selectedDifficulties.ToHashSet(StringComparer.Ordinal);
            var cloScopedQuestions = normalizedApprovedQuestions
                .Where(q => q.CloId.HasValue && cloSet.Contains(q.CloId.Value) && diffSet.Contains(q.Difficulty))
                .ToList();

            var emptyCloCodes = matchedClos
                .Where(c => cloScopedQuestions.All(q => q.CloId != c.Id))
                .Select(c => c.CloCode)
                .ToList();

            if (emptyCloCodes.Count > 0)
            {
                var diffLabels = string.Join(", ", selectedDifficulties.Select(InClassShiftPoolConfig.GetDifficultyLabel));
                return Result<InClassExamShiftResponse>.Failure(
                    $"Thiếu câu hỏi hợp lệ ở {string.Join(", ", emptyCloCodes)} cho mức {diffLabels} (0 câu). Hệ thống không tự bù bằng câu ngoài CLO/mức đã chọn.");
            }

            if (wasExplicitDifficulties)
            {
                var zeroDiffsInClos = selectedDifficulties
                    .Where(d => cloScopedQuestions.All(q => q.Difficulty != d))
                    .ToList();

                if (zeroDiffsInClos.Count > 0)
                {
                    var missingDiffLabels = string.Join(", ", zeroDiffsInClos.Select(InClassShiftPoolConfig.GetDifficultyLabel));
                    return Result<InClassExamShiftResponse>.Failure(
                        $"Các CLO đã chọn chưa có câu hỏi hợp lệ ở mức {missingDiffLabels} (0 câu).");
                }
            }

            if (cloScopedQuestions.Count < request.TotalQuestionsPerStudent)
            {
                var perCloSummary = string.Join(
                    ", ",
                    matchedClos.Select(c => $"{c.CloCode}: {cloScopedQuestions.Count(q => q.CloId == c.Id)} câu"));
                return Result<InClassExamShiftResponse>.Failure(
                    $"Tổng câu hợp lệ của các CLO đã chọn không đủ: cần {request.TotalQuestionsPerStudent} câu mỗi sinh viên nhưng chỉ có {cloScopedQuestions.Count} câu ({perCloSummary}).");
            }
        }

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = actor.Id,
            ShiftName = request.ShiftName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            QuestionPoolMode = normalizedPoolMode,
            SelectedQuestionIdsJson = "[]",
            SelectedDifficultiesJson = InClassShiftPoolConfig.SerializeDifficulties(selectedDifficulties),
            SelectedCloIdsJson = InClassShiftPoolConfig.SerializeGuidList(orderedCloIds),
            TotalQuestionsPerStudent = request.TotalQuestionsPerStudent,
            HasFollowUp = request.HasFollowUp,
            MaxFollowUpQuestions = maxFollowUp,
            IsAutoPublishGrades = request.IsAutoPublishGrades,
            StartTime = startUtc,
            EndTime = endUtc,
            ExamDurationMinutes = examMinutes,
            TotpSecretKey = secretKey,
            Status = "SCHEDULED",
            IsLocked = false,
            CreatedAt = nowUtc
        };

        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync(cancellationToken);

        var response = new InClassExamShiftResponse(
            shift.Id,
            classEntity.Id,
            classEntity.Code,
            classEntity.Name,
            actor.Id,
            actor.FullName,
            shift.ShiftName,
            shift.StartTime,
            shift.EndTime,
            shift.Status,
            shift.IsLocked,
            shift.FinalizedAt,
            shift.CreatedAt,
            ParticipantsCount: 0,
            SubmissionsCount: 0,
            shift.Description,
            shift.QuestionPoolMode,
            Array.Empty<Guid>(),
            shift.TotalQuestionsPerStudent,
            shift.HasFollowUp,
            shift.MaxFollowUpQuestions,
            shift.IsAutoPublishGrades,
            shift.ExamDurationMinutes,
            SelectedDifficulties: selectedDifficulties,
            SelectedCloIds: orderedCloIds,
            SelectedCloCodes: orderedCloCodes,
            CourseCode: classEntity.Course.Code
        );

        return Result<InClassExamShiftResponse>.Success(response);
    }

    private static DateTime ToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            // JSON có offset đã được parser đưa về UTC; Unspecified thuần coi là UTC để khớp CloseDue.
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
