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
    int ExamDurationMinutes = 20) : IRequest<Result>;

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
            var mode = request.QuestionPoolMode?.Trim().ToUpperInvariant();
            if (mode is not ("ALL_COURSE_BANK" or "SELECTED_POOL")) return Result.Failure("Nguồn câu hỏi phải là ALL_COURSE_BANK hoặc SELECTED_POOL.");
            var ids = request.SelectedQuestionIds?.Distinct().ToList() ?? [];
            if (mode == "SELECTED_POOL" && (ids.Contains(Guid.Empty) || ids.Count < request.TotalQuestionsPerStudent))
                return Result.Failure("Danh sách câu hỏi được chọn phải đủ số câu mỗi sinh viên và không chứa ID rỗng.");
            var questions = context.PracticeQuestions.AsNoTracking().Where(x => x.CourseId == shift.Class.CourseId && x.IsActive && x.ApprovalStatus == ApprovalStatus.Approved);
            if (mode == "SELECTED_POOL")
            {
                if (await questions.CountAsync(x => ids.Contains(x.Id), ct) != ids.Count) return Result.Failure(InClassShiftErrors.InvalidCourseQuestion);
            }
            else
            {
                if (await questions.CountAsync(ct) < request.TotalQuestionsPerStudent) return Result.Failure(InClassShiftErrors.InsufficientQuestionPool);
                ids.Clear();
            }
            shift.ShiftName = request.ShiftName.Trim();
            shift.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            shift.StartTime = start;
            shift.EndTime = end;
            shift.QuestionPoolMode = mode;
            shift.SelectedQuestionIdsJson = JsonSerializer.Serialize(ids);
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
