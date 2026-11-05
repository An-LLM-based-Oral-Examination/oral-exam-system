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

        if (actor is null || !actor.IsActive || actor.Role is not (UserRole.Lecturer or UserRole.DepartmentHead or UserRole.Admin))
        {
            return Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var classEntity = await context.Classes
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (classEntity is null || !classEntity.IsActive)
        {
            return Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.ClassNotFound);
        }

        // Nếu là giảng viên thông thường, kiểm tra xem có được phân công phụ trách lớp này không
        if (actor.Role == UserRole.Lecturer)
        {
            bool isAssigned = classEntity.LecturerId == actor.Id ||
                await context.CourseLecturers.AnyAsync(
                    cl => cl.CourseId == classEntity.CourseId && cl.LecturerUserId == actor.Id,
                    cancellationToken);

            if (!isAssigned)
            {
                return Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
            }
        }

        var normalizedPoolMode = string.IsNullOrWhiteSpace(request.QuestionPoolMode)
            ? "ALL_COURSE_BANK"
            : request.QuestionPoolMode.Trim().ToUpperInvariant();

        var selectedIds = request.SelectedQuestionIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? new List<Guid>();

        if (normalizedPoolMode == "SELECTED_POOL")
        {
            if (selectedIds.Count < request.TotalQuestionsPerStudent)
            {
                return Result<InClassExamShiftResponse>.Failure(
                    "Khi chọn chế độ SELECTED_POOL, danh sách câu hỏi nguồn phải có số lượng lớn hơn hoặc bằng số câu mỗi sinh viên.");
            }

            var validApprovedCount = await context.PracticeQuestions
                .AsNoTracking()
                .CountAsync(
                    q => selectedIds.Contains(q.Id) &&
                         q.CourseId == classEntity.CourseId &&
                         q.ApprovalStatus == ApprovalStatus.Approved &&
                         q.IsActive,
                    cancellationToken);

            if (validApprovedCount != selectedIds.Count)
            {
                return Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.InvalidCourseQuestion);
            }
        }

        string secretKey = TotpPasswordHelper.GenerateSecretKey(32);
        int maxFollowUp = request.HasFollowUp ? Math.Clamp(request.MaxFollowUpQuestions <= 0 ? 2 : request.MaxFollowUpQuestions, 1, 5) : 0;

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = actor.Id,
            ShiftName = request.ShiftName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            QuestionPoolMode = normalizedPoolMode,
            SelectedQuestionIdsJson = JsonSerializer.Serialize(selectedIds),
            TotalQuestionsPerStudent = request.TotalQuestionsPerStudent,
            HasFollowUp = request.HasFollowUp,
            MaxFollowUpQuestions = maxFollowUp,
            IsAutoPublishGrades = request.IsAutoPublishGrades,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            TotpSecretKey = secretKey,
            Status = "SCHEDULED",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
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
            selectedIds,
            shift.TotalQuestionsPerStudent,
            shift.HasFollowUp,
            shift.MaxFollowUpQuestions,
            shift.IsAutoPublishGrades
        );

        return Result<InClassExamShiftResponse>.Success(response);
    }
}
