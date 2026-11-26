using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Authorization;
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
                .ThenInclude(c => c.AcademicProgram)
            .Include(c => c.CourseOffering)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (!InClassShiftAcademicContext.IsOperational(classEntity))
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
                e => e.ClassId == classEntity.Id && e.StudentId == actor.Id && (e.Status == "enrolled" || e.Status == "ACTIVE"),
                cancellationToken);

            if (!isEnrolled)
            {
                return Result<IReadOnlyList<InClassExamShiftResponse>>.Failure(InClassShiftErrors.StudentNotEnrolled);
            }
        }
        else if (actor.Role == UserRole.Lecturer)
        {
            bool isAssigned = classEntity.LecturerId == actor.Id;

            if (!isAssigned)
            {
                return Result<IReadOnlyList<InClassExamShiftResponse>>.Failure(InClassShiftErrors.LecturerNotAuthorized);
            }
        }
        else if (actor.Role == UserRole.DepartmentHead)
        {
            if (!AcademicCourseOwnership.IsOwnedBy(classEntity.Course, actor.Id))
                return Result<IReadOnlyList<InClassExamShiftResponse>>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }
        else
        {
            return Result<IReadOnlyList<InClassExamShiftResponse>>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var rawShifts = await context.InClassExamShifts
            .AsNoTracking()
            .Where(s => s.ClassId == request.ClassId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.ClassId,
                s.LecturerId,
                LecturerFullName = s.Lecturer != null ? s.Lecturer.FullName : null,
                s.ShiftName,
                s.StartTime,
                s.EndTime,
                s.Status,
                s.IsLocked,
                s.FinalizedAt,
                s.CreatedAt,
                ParticipantsCount = s.Participants.Count(),
                SubmissionsCount = s.Submissions
                    .Where(submission => !submission.IsFollowUp)
                    .Select(submission => submission.StudentId)
                    .Distinct()
                    .Count(),
                s.Description,
                s.QuestionPoolMode,
                s.SelectedQuestionIdsJson,
                s.TotalQuestionsPerStudent,
                s.HasFollowUp,
                s.MaxFollowUpQuestions,
                s.IsAutoPublishGrades,
                s.ExamDurationMinutes,
                s.SelectedDifficultiesJson,
                s.SelectedCloIdsJson,
                Mine = s.Participants
                    .Where(p => p.StudentId == actor.Id)
                    .Select(p => new
                    {
                        p.Status,
                        p.JoinedAt,
                        p.TotalFinalScore,
                        p.TotalAiScore
                    })
                    .FirstOrDefault(),
                MyMainScores = s.Submissions
                    .Where(sub => sub.StudentId == actor.Id && !sub.IsFollowUp)
                    .Select(sub => sub.FinalScore ?? sub.AiScore ?? 0m)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;

        var allSelectedCloIds = rawShifts
            .SelectMany(s => InClassShiftPoolConfig.ParseGuidList(s.SelectedCloIdsJson))
            .Distinct()
            .ToList();

        var cloCodeMap = allSelectedCloIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.CourseClos
                .AsNoTracking()
                .Where(c => allSelectedCloIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.CloCode, cancellationToken);

        // Luôn trả trạng thái tham gia của user hiện tại (SV) để FE khóa card sau khi nộp bài.
        var shifts = rawShifts
            .Select(s =>
            {
                var effectiveStatus = !s.IsLocked && InClassShiftLifecycle.IsClosable(s.Status) && s.EndTime <= utcNow
                    ? "CLOSED"
                    : s.Status;
                var mine = s.Mine;
                DateTime? joinedAt = mine?.JoinedAt;
                DateTime? examEndsAt = mine is null
                    ? null
                    : InClassShiftLifecycle.GetPersonalExamEndsAt(mine.JoinedAt, s.ExamDurationMinutes, s.EndTime);
                int? remaining = examEndsAt is null
                    ? null
                    : InClassShiftLifecycle.GetRemainingExamSeconds(examEndsAt.Value, utcNow);
                var shiftCloIds = InClassShiftPoolConfig.ParseGuidList(s.SelectedCloIdsJson);
                var shiftCloCodes = shiftCloIds
                    .Where(cloCodeMap.ContainsKey)
                    .Select(id => cloCodeMap[id])
                    .ToList();

                decimal? myFinalScore = null;
                var isFinalized = s.IsLocked || string.Equals(effectiveStatus, "FINALIZED", StringComparison.OrdinalIgnoreCase);
                if (actor.Role == UserRole.Student &&
                    isFinalized &&
                    mine is not null &&
                    !string.Equals(mine.Status, "ABSENT", StringComparison.OrdinalIgnoreCase))
                {
                    if (mine.TotalFinalScore.HasValue)
                    {
                        myFinalScore = Math.Round(mine.TotalFinalScore.Value, 2, MidpointRounding.AwayFromZero);
                    }
                    else if (mine.TotalAiScore.HasValue)
                    {
                        myFinalScore = Math.Round(mine.TotalAiScore.Value, 2, MidpointRounding.AwayFromZero);
                    }
                    else if (s.MyMainScores.Count > 0)
                    {
                        var sumScores = s.MyMainScores.Sum();
                        myFinalScore = Math.Round(
                            sumScores / Math.Max(1, s.TotalQuestionsPerStudent),
                            2,
                            MidpointRounding.AwayFromZero);
                    }
                }

                return new InClassExamShiftResponse(
                    s.Id,
                    s.ClassId,
                    classEntity.Code,
                    classEntity.Name,
                    s.LecturerId,
                    s.LecturerFullName,
                    s.ShiftName,
                    s.StartTime,
                    s.EndTime,
                    effectiveStatus,
                    s.IsLocked,
                    s.FinalizedAt,
                    s.CreatedAt,
                    s.ParticipantsCount,
                    s.SubmissionsCount,
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
                    remaining,
                    InClassShiftPoolConfig.ParseStoredDifficulties(s.SelectedDifficultiesJson),
                    shiftCloIds,
                    shiftCloCodes,
                    classEntity.Course.Code,
                    classEntity.Course.Name,
                    myFinalScore);
            })
            .ToList();

        return Result<IReadOnlyList<InClassExamShiftResponse>>.Success(shifts);
    }
}
