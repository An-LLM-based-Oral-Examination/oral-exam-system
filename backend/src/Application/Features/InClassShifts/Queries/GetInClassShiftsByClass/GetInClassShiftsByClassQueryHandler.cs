using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftsByClass;

public sealed class GetInClassShiftsByClassQueryHandler(
    IApplicationDbContext context)
    : IRequestHandler<GetInClassShiftsByClassQuery, Result<IReadOnlyList<InClassExamShiftResponse>>>
{
    public async Task<Result<IReadOnlyList<InClassExamShiftResponse>>> Handle(
        GetInClassShiftsByClassQuery request,
        CancellationToken cancellationToken)
    {
        var classEntity = await context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (classEntity is null)
        {
            return Result<IReadOnlyList<InClassExamShiftResponse>>.Failure(InClassShiftErrors.ClassNotFound);
        }

        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive)
        {
            return Result<IReadOnlyList<InClassExamShiftResponse>>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        if (actor.Role == UserRole.Student)
        {
            bool isEnrolled = await context.ClassEnrollments.AnyAsync(
                e => e.ClassId == classEntity.Id && e.StudentId == actor.Id && e.Status == "enrolled",
                cancellationToken);

            if (!isEnrolled)
            {
                return Result<IReadOnlyList<InClassExamShiftResponse>>.Failure(InClassShiftErrors.StudentNotEnrolled);
            }
        }
        else if (actor.Role == UserRole.Lecturer)
        {
            bool isAssigned = classEntity.LecturerId == actor.Id ||
                await context.CourseLecturers.AnyAsync(
                    cl => cl.CourseId == classEntity.CourseId && cl.LecturerUserId == actor.Id,
                    cancellationToken);

            if (!isAssigned)
            {
                return Result<IReadOnlyList<InClassExamShiftResponse>>.Failure(InClassShiftErrors.LecturerNotAuthorized);
            }
        }

        await InClassShiftLifecycle.CloseDueAsync(
            context,
            DateTime.UtcNow,
            cancellationToken,
            classId: request.ClassId);

        var rawShifts = await context.InClassExamShifts
            .AsNoTracking()
            .Include(s => s.Lecturer)
            .Include(s => s.Participants)
            .Include(s => s.Submissions)
            .Where(s => s.ClassId == request.ClassId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;

        // Luôn trả trạng thái tham gia của user hiện tại (SV) để FE khóa card sau khi nộp bài.
        var shifts = rawShifts
            .Select(s =>
            {
                var mine = s.Participants.FirstOrDefault(p => p.StudentId == actor.Id);
                DateTime? joinedAt = mine?.JoinedAt;
                DateTime? examEndsAt = mine is null
                    ? null
                    : InClassShiftLifecycle.GetPersonalExamEndsAt(mine.JoinedAt, s.ExamDurationMinutes);
                int? remaining = examEndsAt is null
                    ? null
                    : InClassShiftLifecycle.GetRemainingExamSeconds(examEndsAt.Value, utcNow);

                return new InClassExamShiftResponse(
                    s.Id,
                    s.ClassId,
                    classEntity.Code,
                    classEntity.Name,
                    s.LecturerId,
                    s.Lecturer != null ? s.Lecturer.FullName : null,
                    s.ShiftName,
                    s.StartTime,
                    s.EndTime,
                    s.Status,
                    s.IsLocked,
                    s.FinalizedAt,
                    s.CreatedAt,
                    s.Participants.Count,
                    s.Participants.Count(p => p.Status == "SUBMITTED"),
                    s.Description,
                    s.QuestionPoolMode,
                    EnterInClassShiftCommandHandler.ParseGuidList(s.SelectedQuestionIdsJson),
                    s.TotalQuestionsPerStudent,
                    s.HasFollowUp,
                    s.MaxFollowUpQuestions,
                    s.IsAutoPublishGrades,
                    s.ExamDurationMinutes,
                    mine?.Status,
                    joinedAt,
                    examEndsAt,
                    remaining);
            })
            .ToList();

        return Result<IReadOnlyList<InClassExamShiftResponse>>.Success(shifts);
    }
}
