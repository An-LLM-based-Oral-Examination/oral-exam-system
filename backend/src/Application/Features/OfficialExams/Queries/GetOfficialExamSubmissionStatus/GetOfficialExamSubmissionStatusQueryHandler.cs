using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSubmissionStatus;

public sealed class GetOfficialExamSubmissionStatusQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetOfficialExamSubmissionStatusQuery, Result<OfficialExamSubmissionStatusResponse>>
{
    public async Task<Result<OfficialExamSubmissionStatusResponse>> Handle(
        GetOfficialExamSubmissionStatusQuery request,
        CancellationToken cancellationToken)
    {
        var ticket = await context.StudentExamTickets
            .AsNoTracking()
            .Include(x => x.Submissions)
            .FirstOrDefaultAsync(x => x.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<OfficialExamSubmissionStatusResponse>.Failure(OfficialExamErrors.TicketNotFound);
        }

        if (ticket.StudentId != request.ActorUserId)
        {
            return Result<OfficialExamSubmissionStatusResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        return Result<OfficialExamSubmissionStatusResponse>.Success(new(
            ticket.Id,
            ticket.Status,
            ticket.SubmittedAt,
            ticket.Submissions.Count,
            ticket.Submissions.Count(x => x.GradingStatus is "pending" or "transcribing" or "transcribed"),
            ticket.Submissions.Count(x => x.GradingStatus is "ai_graded" or "audited"),
            ticket.Submissions.Count(x => x.GradingStatus == "failed")));
    }
}
