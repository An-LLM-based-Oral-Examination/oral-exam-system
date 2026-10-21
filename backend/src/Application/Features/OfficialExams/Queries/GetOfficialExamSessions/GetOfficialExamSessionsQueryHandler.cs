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

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSessions;

/// <summary>
/// Handler truy vấn danh sách kỳ thi thật (OfficialExamSession).
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

        var sessions = await query
            .OrderByDescending(s => s.ExamDate)
            .ThenByDescending(s => s.CreatedAt)
            .Select(s => new OfficialExamSessionResponse(
                s.Id,
                s.CourseId,
                s.ExamStructureId,
                s.Title,
                s.ExamDate,
                s.HasFollowUp,
                s.MaxFollowUpQuestions,
                s.ExamInputMode,
                s.TranscriptBufferSeconds,
                s.Status))
            .ToListAsync(cancellationToken);

        return Result<List<OfficialExamSessionResponse>>.Success(sessions);
    }
}
