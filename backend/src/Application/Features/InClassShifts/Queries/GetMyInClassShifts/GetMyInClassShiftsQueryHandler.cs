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

namespace OralExamination.Application.Features.InClassShifts.Queries.GetMyInClassShifts;

public sealed class GetMyInClassShiftsQueryHandler(
    IApplicationDbContext context)
    : IRequestHandler<GetMyInClassShiftsQuery, Result<IReadOnlyList<InClassExamShiftResponse>>>
{
    public async Task<Result<IReadOnlyList<InClassExamShiftResponse>>> Handle(
        GetMyInClassShiftsQuery request,
        CancellationToken cancellationToken)
    {
        var enrolledClasses = await context.ClassEnrollments
            .AsNoTracking()
            .Where(e =>
                e.StudentId == request.StudentUserId &&
                (e.Status == "enrolled" || e.Status == "ACTIVE") &&
                e.Class.IsActive &&
                e.Class.Course.IsActive &&
                (e.Class.CourseOffering == null || e.Class.CourseOffering.IsEnabled))
            .Include(e => e.Class)
                .ThenInclude(c => c.Course)
            .Include(e => e.Class)
                .ThenInclude(c => c.CourseOffering)
            .Select(e => new
            {
                e.ClassId,
                ClassCode = e.Class != null ? e.Class.Code : string.Empty,
                ClassName = e.Class != null ? e.Class.Name : string.Empty,
                CourseCode = e.Class != null && e.Class.Course != null ? e.Class.Course.Code : string.Empty,
                CourseName = e.Class != null && e.Class.Course != null ? e.Class.Course.Name : string.Empty
            })
            .ToListAsync(cancellationToken);

        if (enrolledClasses.Count == 0)
        {
            return Result<IReadOnlyList<InClassExamShiftResponse>>.Success(Array.Empty<InClassExamShiftResponse>());
        }

        var classMap = enrolledClasses
            .GroupBy(c => c.ClassId)
            .ToDictionary(g => g.Key, g => g.First());

        var classIds = classMap.Keys.ToList();

        foreach (var classId in classIds)
        {
            await InClassShiftLifecycle.CloseDueAsync(
                context,
                DateTime.UtcNow,
                cancellationToken,
                classId: classId);
        }

        var rawShifts = await context.InClassExamShifts
            .AsNoTracking()
            .Where(s => classIds.Contains(s.ClassId))
            .OrderByDescending(s => s.StartTime)
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
                    .Where(p => p.StudentId == request.StudentUserId)
                    .Select(p => new
                    {
                        p.Status,
                        p.JoinedAt,
                        p.TotalFinalScore,
                        p.TotalAiScore
                    })
                    .FirstOrDefault(),
                MyMainScores = s.Submissions
                    .Where(sub => sub.StudentId == request.StudentUserId && !sub.IsFollowUp)
                    .Select(sub => sub.FinalScore ?? sub.AiScore ?? 0m)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

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

        var utcNow = DateTime.UtcNow;

        var shifts = rawShifts
            .Select(s =>
            {
                var classInfo = classMap.TryGetValue(s.ClassId, out var found) ? found : null;
                var mine = s.Mine;
                DateTime? joinedAt = mine?.JoinedAt;
                DateTime? examEndsAt = mine is null
                    ? null
                    : InClassShiftLifecycle.GetPersonalExamEndsAt(mine.JoinedAt, s.ExamDurationMinutes);
                int? remaining = examEndsAt is null
                    ? null
                    : InClassShiftLifecycle.GetRemainingExamSeconds(examEndsAt.Value, utcNow);
                var shiftCloIds = InClassShiftPoolConfig.ParseGuidList(s.SelectedCloIdsJson);
                var shiftCloCodes = shiftCloIds
                    .Where(cloCodeMap.ContainsKey)
                    .Select(id => cloCodeMap[id])
                    .ToList();

                decimal? myFinalScore = null;
                var isFinalized = s.IsLocked || string.Equals(s.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase);
                if (isFinalized && mine is not null)
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
                    classInfo?.ClassCode ?? string.Empty,
                    classInfo?.ClassName ?? string.Empty,
                    s.LecturerId,
                    s.LecturerFullName,
                    s.ShiftName,
                    s.StartTime,
                    s.EndTime,
                    s.Status,
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
                    classInfo?.CourseCode,
                    classInfo?.CourseName,
                    myFinalScore
                );
            })
            .ToList();

        return Result<IReadOnlyList<InClassExamShiftResponse>>.Success(shifts);
    }
}
