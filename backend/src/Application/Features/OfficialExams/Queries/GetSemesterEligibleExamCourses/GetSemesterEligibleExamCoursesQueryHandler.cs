using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetSemesterEligibleExamCourses;

public sealed class GetSemesterEligibleExamCoursesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSemesterEligibleExamCoursesQuery, Result<IReadOnlyList<SemesterEligibleExamCourseDto>>>
{
    public async Task<Result<IReadOnlyList<SemesterEligibleExamCourseDto>>> Handle(
        GetSemesterEligibleExamCoursesQuery request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveOfficerAsync(context, request.ActorUserId, cancellationToken))
        {
            return Result<IReadOnlyList<SemesterEligibleExamCourseDto>>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        if (request.SemesterId == Guid.Empty)
        {
            return Result<IReadOnlyList<SemesterEligibleExamCourseDto>>.Failure(OfficialExamErrors.SemesterNotFound);
        }

        var semester = await context.Semesters
            .AsNoTracking()
            .Where(item => item.Id == request.SemesterId)
            .Select(item => new { item.Id, item.Code })
            .FirstOrDefaultAsync(cancellationToken);

        if (semester is null)
        {
            return Result<IReadOnlyList<SemesterEligibleExamCourseDto>>.Failure(OfficialExamErrors.SemesterNotFound);
        }

        var eligibleCourses = await OfficialExamSchedulingHelper.GetEligibleCoursesForSemesterAsync(
            context, semester.Id, cancellationToken);

        if (eligibleCourses.Count == 0)
        {
            return Result<IReadOnlyList<SemesterEligibleExamCourseDto>>.Success(Array.Empty<SemesterEligibleExamCourseDto>());
        }

        var courseIds = eligibleCourses.Select(course => course.Id).ToList();

        var activeClasses = await context.Classes
            .AsNoTracking()
            .Where(cls =>
                cls.IsActive
                && cls.SemesterId == semester.Id
                && courseIds.Contains(cls.CourseId)
                && (cls.CourseOffering == null || cls.CourseOffering.IsEnabled))
            .Select(cls => new { cls.Id, cls.CourseId })
            .ToListAsync(cancellationToken);

        var activeClassCountByCourse = activeClasses
            .GroupBy(cls => cls.CourseId)
            .ToDictionary(group => group.Key, group => group.Count());

        var enrolledStudents = await context.ClassEnrollments
            .AsNoTracking()
            .Where(enrollment =>
                enrollment.Status == "enrolled"
                && enrollment.Student.IsActive
                && enrollment.Student.Role == UserRole.Student
                && enrollment.Class.IsActive
                && enrollment.Class.SemesterId == semester.Id
                && courseIds.Contains(enrollment.Class.CourseId)
                && enrollment.Class.Course.IsActive
                && (enrollment.Class.CourseOffering == null || enrollment.Class.CourseOffering.IsEnabled))
            .Select(enrollment => new { enrollment.Class.CourseId, enrollment.StudentId })
            .ToListAsync(cancellationToken);

        var eligibleStudentCountByCourse = enrolledStudents
            .GroupBy(item => item.CourseId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.StudentId).Distinct().Count());

        var activeStructures = await context.ExamStructures
            .AsNoTracking()
            .Where(structure => courseIds.Contains(structure.CourseId) && structure.IsActive)
            .OrderByDescending(structure => structure.CreatedAt)
            .ThenBy(structure => structure.Id)
            .Select(structure => new { structure.Id, structure.CourseId, structure.Name })
            .ToListAsync(cancellationToken);

        var activeStructureByCourse = activeStructures
            .GroupBy(structure => structure.CourseId)
            .ToDictionary(group => group.Key, group => group.First());

        var sessionsInSemester = await context.OfficialExamSessions
            .AsNoTracking()
            .Where(session =>
                courseIds.Contains(session.CourseId)
                && (session.SemesterId == semester.Id
                    || (session.SemesterId == Guid.Empty && session.Course.SemesterId == semester.Id)))
            .OrderByDescending(session => session.CreatedAt)
            .ThenByDescending(session => session.ExamDate)
            .Select(session => new
            {
                session.Id,
                session.CourseId,
                session.ExamAttemptType,
                session.Title,
                session.Status
            })
            .ToListAsync(cancellationToken);

        var sessionIds = sessionsInSemester.Select(s => s.Id).ToList();

        var candidateCountsBySession = sessionIds.Count == 0
            ? new Dictionary<Guid, int>()
            : (await context.OfficialExamCandidates
                .AsNoTracking()
                .Where(c => sessionIds.Contains(c.SessionId))
                .GroupBy(c => c.SessionId)
                .Select(g => new { SessionId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.SessionId, x => x.Count);

        var sessionShifts = sessionIds.Count == 0
            ? []
            : await context.RealExamSessionShifts
                .AsNoTracking()
                .Where(sh => sessionIds.Contains(sh.SessionId))
                .Select(sh => new { sh.Id, sh.SessionId })
                .ToListAsync(cancellationToken);

        var shiftToSession = sessionShifts.ToDictionary(sh => sh.Id, sh => sh.SessionId);
        var allShiftIds = sessionShifts.Select(sh => sh.Id).ToList();

        var allocatedCountsBySession = allShiftIds.Count == 0
            ? new Dictionary<Guid, int>()
            : (await context.StudentExamTickets
                .AsNoTracking()
                .Where(t => allShiftIds.Contains(t.ShiftId))
                .Select(t => new { t.ShiftId, t.StudentId })
                .ToListAsync(cancellationToken))
                .GroupBy(t => shiftToSession[t.ShiftId])
                .ToDictionary(g => g.Key, g => g.Select(x => x.StudentId).Distinct().Count());

        var feSessionByCourse = sessionsInSemester
            .Where(s => string.Equals(s.ExamAttemptType, OfficialExamAttemptType.FinalExam, StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => s.CourseId)
            .ToDictionary(g => g.Key, g => g.First());

        var reSessionByCourse = sessionsInSemester
            .Where(s => string.Equals(s.ExamAttemptType, OfficialExamAttemptType.RetakeExam, StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => s.CourseId)
            .ToDictionary(g => g.Key, g => g.First());

        var handedOverSets = await context.ExamSets
            .AsNoTracking()
            .Where(set =>
                courseIds.Contains(set.CourseId)
                && set.IsActive
                && !set.IsSampleSet
                && set.Status == ExamSetStatus.HandedOver)
            .Select(set => new { set.Id, set.CourseId, set.SessionId })
            .ToListAsync(cancellationToken);

        var results = new List<SemesterEligibleExamCourseDto>(eligibleCourses.Count);
        foreach (var course in eligibleCourses)
        {
            var classCount = activeClassCountByCourse.GetValueOrDefault(course.Id, 0);
            var studentCount = eligibleStudentCountByCourse.GetValueOrDefault(course.Id, 0);
            activeStructureByCourse.TryGetValue(course.Id, out var activeStructure);
            feSessionByCourse.TryGetValue(course.Id, out var feSessionEntity);
            reSessionByCourse.TryGetValue(course.Id, out var reSessionEntity);
            var primarySession = feSessionEntity ?? reSessionEntity;

            var feSummary = feSessionEntity is null
                ? null
                : new SemesterCourseAttemptSessionSummaryDto(
                    feSessionEntity.Id,
                    OfficialExamAttemptType.FinalExam,
                    feSessionEntity.Title,
                    feSessionEntity.Status,
                    candidateCountsBySession.GetValueOrDefault(feSessionEntity.Id, 0),
                    allocatedCountsBySession.GetValueOrDefault(feSessionEntity.Id, 0));

            var reSummary = reSessionEntity is null
                ? null
                : new SemesterCourseAttemptSessionSummaryDto(
                    reSessionEntity.Id,
                    OfficialExamAttemptType.RetakeExam,
                    reSessionEntity.Title,
                    reSessionEntity.Status,
                    candidateCountsBySession.GetValueOrDefault(reSessionEntity.Id, 0),
                    allocatedCountsBySession.GetValueOrDefault(reSessionEntity.Id, 0));

            var handedOverCount = handedOverSets.Count(set =>
                set.CourseId == course.Id
                && ((feSessionEntity is not null && set.SessionId == feSessionEntity.Id)
                    || (reSessionEntity is not null && set.SessionId == reSessionEntity.Id)));

            var clampedMaxFollowUp = Math.Clamp(
                course.MaxFollowUpQuestions,
                OfficialExamRules.MinFollowUpQuestions,
                OfficialExamRules.MaxFollowUpQuestions);

            results.Add(new SemesterEligibleExamCourseDto(
                course.Id,
                course.Code,
                course.Name,
                semester.Id,
                semester.Code,
                classCount,
                studentCount,
                OfficialExamRules.DefaultShiftCapacity,
                OfficialExamSchedulingHelper.ComputeRecommendedRoomCount(studentCount),
                activeStructure?.Id,
                activeStructure?.Name,
                handedOverCount,
                course.HasFollowUp,
                clampedMaxFollowUp,
                primarySession?.Id,
                primarySession?.Title,
                primarySession?.Status,
                feSummary,
                reSummary));
        }

        return Result<IReadOnlyList<SemesterEligibleExamCourseDto>>.Success(results);
    }
}
