using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSessions;

/// <summary>
/// Handler truy vấn danh sách kỳ thi thật (OfficialExamSession) có lọc theo môn, học kỳ, đợt FE/RE và trạng thái.
/// Áp dụng fail-closed role scoping theo vai trò đã xác thực trong DB.
/// </summary>
public sealed class GetOfficialExamSessionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetOfficialExamSessionsQuery, Result<List<OfficialExamSessionResponse>>>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.ExaminationOfficer,
        UserRole.DepartmentHead,
        UserRole.Lecturer,
        UserRole.Proctor
    ];

    private readonly IApplicationDbContext _context = context;

    public async Task<Result<List<OfficialExamSessionResponse>>> Handle(
        GetOfficialExamSessionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorRole))
        {
            return Result<List<OfficialExamSessionResponse>>.Failure(OfficialExamErrors.Forbidden);
        }

        var claimedRole = request.ActorRole.Trim();
        if (!AllowedRoles.Contains(claimedRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result<List<OfficialExamSessionResponse>>.Failure(OfficialExamErrors.Forbidden);
        }

        var actor = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null ||
            !AllowedRoles.Contains(actor.Role, StringComparer.OrdinalIgnoreCase) ||
            !string.Equals(actor.Role, claimedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<List<OfficialExamSessionResponse>>.Failure(OfficialExamErrors.Forbidden);
        }

        var query = _context.OfficialExamSessions.AsNoTracking().AsQueryable();

        if (string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
        {
            var ownedCourseIds = _context.Courses
                .AsNoTracking()
                .Where(c =>
                    (c.AcademicProgram != null && c.AcademicProgram.DepartmentHeadUserId == actor.Id)
                    || ((c.AcademicProgram == null
                        || c.AcademicProgram.DepartmentHeadUserId == null
                        || c.AcademicProgram.DepartmentHeadUserId == Guid.Empty)
                        && c.DepartmentHeadUserId == actor.Id))
                .Select(c => c.Id);

            query = query.Where(s => ownedCourseIds.Contains(s.CourseId));
        }
        else if (string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
        {
            var assignedCourseIds = _context.CourseLecturers
                .AsNoTracking()
                .Where(cl =>
                    cl.LecturerUserId == actor.Id
                    && cl.IsActive
                    && cl.Course != null
                    && cl.Course.IsActive
                    && (cl.CourseOffering == null || cl.CourseOffering.IsEnabled))
                .Select(cl => cl.CourseId)
                .Union(_context.Classes
                    .AsNoTracking()
                    .Where(c =>
                        c.LecturerId == actor.Id
                        && c.IsActive
                        && c.Course != null
                        && c.Course.IsActive
                        && (c.CourseOffering == null || c.CourseOffering.IsEnabled))
                    .Select(c => c.CourseId));

            var assignedShiftSessionIds = _context.RealExamSessionShifts
                .AsNoTracking()
                .Where(sh => sh.ExaminerUserId == actor.Id || sh.ProctorUserId == actor.Id)
                .Select(sh => sh.SessionId);

            query = query.Where(s =>
                assignedCourseIds.Contains(s.CourseId)
                || assignedShiftSessionIds.Contains(s.Id));
        }
        else if (string.Equals(actor.Role, UserRole.Proctor, StringComparison.OrdinalIgnoreCase))
        {
            var proctorSessionIds = _context.RealExamSessionShifts
                .AsNoTracking()
                .Where(sh => sh.ProctorUserId == actor.Id)
                .Select(sh => sh.SessionId);

            query = query.Where(s => proctorSessionIds.Contains(s.Id));
        }

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            query = query.Where(s => s.CourseId == request.CourseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var normalizedStatus = request.Status.Trim().ToLowerInvariant();
            query = query.Where(s => s.Status.ToLower() == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.ExamAttemptType))
        {
            var normalizedAttempt = request.ExamAttemptType.Trim().ToUpperInvariant();
            if (!OfficialExamAttemptType.IsValid(normalizedAttempt))
            {
                return Result<List<OfficialExamSessionResponse>>.Failure(
                    "BAD_REQUEST: Loại đợt thi (examAttemptType) chỉ chấp nhận FE hoặc RE.");
            }

            query = query.Where(s => s.ExamAttemptType == normalizedAttempt);
        }

        var rawSessions = await query
            .OrderByDescending(s => s.ExamDate)
            .ThenByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.CourseId,
                s.ExamStructureId,
                s.Title,
                s.ExamDate,
                s.HasFollowUp,
                s.MaxFollowUpQuestions,
                s.ExamInputMode,
                s.TranscriptBufferSeconds,
                s.Status,
                s.SemesterId,
                s.ExamAttemptType
            })
            .ToListAsync(cancellationToken);

        var courseIds = rawSessions.Select(s => s.CourseId).Distinct().ToList();
        var coursesById = courseIds.Count == 0
            ? new Dictionary<Guid, (string Code, string Name, Guid SemesterId)>()
            : (await _context.Courses
                .AsNoTracking()
                .Where(c => courseIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Code, c.Name, c.SemesterId })
                .ToListAsync(cancellationToken))
                .ToDictionary(c => c.Id, c => (c.Code, c.Name, c.SemesterId));

        if (request.SemesterId.HasValue && request.SemesterId.Value != Guid.Empty)
        {
            var filterSemId = request.SemesterId.Value;
            rawSessions = rawSessions
                .Where(s =>
                    s.SemesterId == filterSemId
                    || (s.SemesterId == Guid.Empty
                        && coursesById.TryGetValue(s.CourseId, out var c)
                        && c.SemesterId == filterSemId))
                .ToList();
        }

        var sessionIds = rawSessions.Select(s => s.Id).ToList();

        var candidateIdsBySession = sessionIds.Count == 0
            ? new Dictionary<Guid, HashSet<Guid>>()
            : (await _context.OfficialExamCandidates
                .AsNoTracking()
                .Where(c => sessionIds.Contains(c.SessionId))
                .Select(c => new { c.SessionId, c.StudentId })
                .ToListAsync(cancellationToken))
                .GroupBy(c => c.SessionId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.StudentId).ToHashSet());

        var shifts = sessionIds.Count == 0
            ? []
            : await _context.RealExamSessionShifts
                .AsNoTracking()
                .Where(sh => sessionIds.Contains(sh.SessionId))
                .Select(sh => new { sh.Id, sh.SessionId })
                .ToListAsync(cancellationToken);

        var shiftToSession = shifts.ToDictionary(sh => sh.Id, sh => sh.SessionId);
        var shiftIds = shifts.Select(sh => sh.Id).ToList();

        var allocatedStudentsBySession = shiftIds.Count == 0
            ? new Dictionary<Guid, HashSet<Guid>>()
            : (await _context.StudentExamTickets
                .AsNoTracking()
                .Where(t => shiftIds.Contains(t.ShiftId))
                .Select(t => new { t.ShiftId, t.StudentId })
                .ToListAsync(cancellationToken))
                .GroupBy(t => shiftToSession[t.ShiftId])
                .ToDictionary(g => g.Key, g => g.Select(x => x.StudentId).ToHashSet());

        var sessions = rawSessions.Select(s =>
        {
            var hasCourse = coursesById.TryGetValue(s.CourseId, out var course);
            Guid? resolvedSemesterId = s.SemesterId != Guid.Empty
                ? s.SemesterId
                : (hasCourse && course.SemesterId != Guid.Empty ? course.SemesterId : null);
            var candidates = candidateIdsBySession.GetValueOrDefault(s.Id);
            var allocated = allocatedStudentsBySession.GetValueOrDefault(s.Id);
            var candidateCount = candidates?.Count ?? 0;
            var allocatedCandidateCount = candidates is null || allocated is null
                ? 0
                : candidates.Count(allocated.Contains);

            return new OfficialExamSessionResponse(
                s.Id,
                s.CourseId,
                s.ExamStructureId,
                s.Title,
                s.ExamDate,
                s.HasFollowUp,
                s.MaxFollowUpQuestions,
                s.ExamInputMode,
                s.TranscriptBufferSeconds,
                s.Status,
                resolvedSemesterId,
                string.IsNullOrWhiteSpace(s.ExamAttemptType) ? OfficialExamAttemptType.FinalExam : s.ExamAttemptType,
                hasCourse ? course.Code : null,
                hasCourse ? course.Name : null,
                candidateCount,
                allocatedCandidateCount);
        }).ToList();

        return Result<List<OfficialExamSessionResponse>>.Success(sessions);
    }
}
