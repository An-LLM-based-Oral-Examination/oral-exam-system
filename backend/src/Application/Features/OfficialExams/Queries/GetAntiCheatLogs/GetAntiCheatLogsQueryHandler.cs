using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetAntiCheatLogs;

public sealed class GetAntiCheatLogsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAntiCheatLogsQuery, Result<List<ExamAntiCheatLogDto>>>
{
    public async Task<Result<List<ExamAntiCheatLogDto>>> Handle(GetAntiCheatLogsQuery request, CancellationToken cancellationToken)
    {
        var query = context.ExamAntiCheatLogs
            .AsNoTracking()
            .Where(l => l.ShiftId == request.ShiftId);

        if (request.TicketId.HasValue)
        {
            query = query.Where(l => l.TicketId == request.TicketId.Value);
        }

        var logs = await query
            .OrderByDescending(l => l.OccurredAt)
            .Select(l => new ExamAntiCheatLogDto
            {
                Id = l.Id,
                TicketId = l.TicketId,
                ShiftId = l.ShiftId,
                StudentId = l.StudentId,
                StudentCode = l.Student.StudentCode ?? string.Empty,
                StudentName = l.Student.FullName,
                EventType = l.EventType,
                Severity = l.Severity,
                Details = l.Details,
                ViolationCount = l.ViolationCount,
                OccurredAt = l.OccurredAt
            })
            .ToListAsync(cancellationToken);

        return Result<List<ExamAntiCheatLogDto>>.Success(logs);
    }
}
