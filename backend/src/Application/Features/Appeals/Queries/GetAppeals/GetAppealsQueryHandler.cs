using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;

namespace OralExamination.Application.Features.Appeals.Queries.GetAppeals;

public sealed class GetAppealsQueryHandler : IRequestHandler<GetAppealsQuery, Result<List<AppealResponseDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAppealsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AppealResponseDto>>> Handle(GetAppealsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AppealRequests
            .AsNoTracking()
            .Include(a => a.Ticket)
                .ThenInclude(t => t.Shift)
                    .ThenInclude(s => s.Session)
            .Include(a => a.Student)
            .Include(a => a.AssignedToUser)
            .Include(a => a.ReviewedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(a => a.Status == request.Status.Trim());
        }

        if (request.SessionId.HasValue)
        {
            query = query.Where(a => a.SessionId == request.SessionId.Value);
        }

        if (request.StudentId.HasValue)
        {
            query = query.Where(a => a.StudentId == request.StudentId.Value);
        }

        if (request.AssignedTo.HasValue)
        {
            query = query.Where(a => a.AssignedTo == request.AssignedTo.Value);
        }

        var appeals = await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = appeals.Select(a => new AppealResponseDto
        {
            Id = a.Id,
            TicketId = a.TicketId,
            StudentId = a.StudentId,
            StudentCode = a.Student?.StudentCode ?? string.Empty,
            StudentName = a.Student?.FullName ?? string.Empty,
            SessionId = a.SessionId,
            SessionTitle = a.Ticket?.Shift?.Session?.Title ?? string.Empty,
            SubmissionId = a.SubmissionId,
            Reason = a.Reason,
            Status = a.Status,
            AssignedTo = a.AssignedTo,
            AssignedToName = a.AssignedToUser?.FullName ?? string.Empty,
            ReviewedBy = a.ReviewedBy,
            ReviewedByName = a.ReviewedByUser?.FullName,
            Decision = a.Decision,
            OriginalScore = a.OriginalScore,
            ProposedScore = a.ProposedScore,
            ReviewNotes = a.ReviewNotes,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt,
            ResolvedAt = a.ResolvedAt
        }).ToList();

        return Result<List<AppealResponseDto>>.Success(dtos);
    }
}
