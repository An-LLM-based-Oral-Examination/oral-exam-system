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
                && session.Status.ToLower() != "cancelled"
                && (session.SemesterId == semester.Id
                    || (session.SemesterId == null && session.Course.SemesterId == semester.Id)))
            .OrderByDescending(session => session.CreatedAt)
            .ThenByDescending(session => session.ExamDate)
            .Select(session => new
            {
                session.Id,
                session.CourseId,
                session.Title,
                session.Status
            })
            .ToListAsync(cancellationToken);

        var existingSessionByCourse = sessionsInSemester
            .GroupBy(session => session.CourseId)
            .ToDictionary(group => group.Key, group => group.First());

        var handedOverSets = await context.ExamSets
            .AsNoTracking()
            .Where(set =>
                courseIds.Contains(set.CourseId)
                && set.IsActive
                && set.Status == ExamSetStatus.HandedOver)
            .Select(set => new { set.Id, set.CourseId, set.SessionId })
            .ToListAsync(cancellationToken);

        var results = new List<SemesterEligibleExamCourseDto>(eligibleCourses.Count);
        foreach (var course in eligibleCourses)
        {
            var classCount = activeClassCountByCourse.GetValueOrDefault(course.Id, 0);
            var studentCount = eligibleStudentCountByCourse.GetValueOrDefault(course.Id, 0);
            activeStructureByCourse.TryGetValue(course.Id, out var activeStructure);
            existingSessionByCourse.TryGetValue(course.Id, out var existingSession);

            var handedOverCount = handedOverSets.Count(set =>
                set.CourseId == course.Id
                && (!set.SessionId.HasValue
                    || (existingSession is not null && set.SessionId.Value == existingSession.Id)));

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
                existingSession?.Id,
                existingSession?.Title,
                existingSession?.Status));
        }

        return Result<IReadOnlyList<SemesterEligibleExamCourseDto>>.Success(results);
    }
}
