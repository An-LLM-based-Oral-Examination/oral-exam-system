using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSessions;

/// <summary>
/// Handler truy vấn danh sách kỳ thi thật (OfficialExamSession) có lọc theo môn, học kỳ, đợt FE/RE và trạng thái.
/// </summary>
public sealed class GetOfficialExamSessionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetOfficialExamSessionsQuery, Result<List<OfficialExamSessionResponse>>>
{
    private readonly IApplicationDbContext _context = context;

    public async Task<Result<List<OfficialExamSessionResponse>>> Handle(
        GetOfficialExamSessionsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.OfficialExamSessions.AsNoTracking().AsQueryable();

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
