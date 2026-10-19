using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;

public sealed class SubmitOfficialExamCommandHandler(IApplicationDbContext context)
    : IRequestHandler<SubmitOfficialExamCommand, Result<SubmitOfficialExamResponse>>
{
    public async Task<Result<SubmitOfficialExamResponse>> Handle(
        SubmitOfficialExamCommand request,
        CancellationToken cancellationToken)
    {
        var ticket = await context.StudentExamTickets
            .Include(x => x.Submissions)
            .FirstOrDefaultAsync(x => x.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.TicketNotFound);
        }

        if (ticket.StudentId != request.ActorUserId)
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (!WorkstationAddress.Same(ticket.IpAddress, request.RemoteIpAddress))
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.WrongWorkstation);
        }

        if (ticket.Status is ExamTicketStatus.Submitted or ExamTicketStatus.AiGraded or ExamTicketStatus.Audited or ExamTicketStatus.Published or ExamTicketStatus.Locked)
        {
            if (ticket.SubmittedAt.HasValue && ticket.Submissions.Count > 0)
            {
                return Result<SubmitOfficialExamResponse>.Success(new(
                    ticket.Id,
                    ticket.Status,
                    ticket.SubmittedAt.Value,
                    ticket.Submissions.Count));
            }

            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.SubmissionAlreadyExists);
        }

        if (!string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.TicketNotInProgress);
        }

        if (!ticket.AudioVerifiedAt.HasValue ||
            string.IsNullOrWhiteSpace(ticket.AudioObjectKey) ||
            string.IsNullOrWhiteSpace(ticket.AudioHashSha256))
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.AudioNotSealed);
        }

        if (!ticket.ExamSetId.HasValue)
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.ExamSetNotAssigned);
        }

        var answers = request.Answers!;
        var duplicateQuestionIds = answers
            .GroupBy(x => x.ExamQuestionId)
            .Any(group => group.Count() > 1);
        if (duplicateQuestionIds)
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.SubmissionQuestionsMismatch);
        }

        var expectedQuestionIds = await context.ExamSetQuestions
            .AsNoTracking()
            .Where(x => x.ExamSetId == ticket.ExamSetId.Value)
            .Select(x => x.ExamQuestionId)
            .ToListAsync(cancellationToken);
        var submittedQuestionIds = answers.Select(x => x.ExamQuestionId).ToHashSet();
        if (expectedQuestionIds.Count == 0 ||
            expectedQuestionIds.Count != submittedQuestionIds.Count ||
            expectedQuestionIds.Any(id => !submittedQuestionIds.Contains(id)))
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.SubmissionQuestionsMismatch);
        }

        var now = DateTime.UtcNow;
        foreach (var answer in answers)
        {
            var submission = new ExamQuestionSubmission
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                ExamQuestionId = answer.ExamQuestionId,
                AudioR2Url = ticket.AudioObjectKey,
                AudioHashSha256 = ticket.AudioHashSha256,
                TranscriptWhisper = answer.TranscriptWhisper!.Trim(),
                TimestampsWhisper = string.IsNullOrWhiteSpace(answer.TimestampsWhisper)
                    ? null
                    : answer.TimestampsWhisper,
                TimeSpentSeconds = answer.TimeSpentSeconds,
                GradingStatus = "pending",
                SubmittedAt = now
            };

            context.ExamQuestionSubmissions.Add(submission);
            context.GradingJobs.Add(new GradingJob
            {
                Id = Guid.NewGuid(),
                SubmissionId = submission.Id,
                Status = "pending",
                AttemptCount = 0,
                NextAttemptAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        ticket.Status = ExamTicketStatus.Submitted;
        ticket.SubmittedAt = now;

        // EF Core wraps this SaveChanges in one database transaction: submissions,
        // durable grading jobs and ticket state either all commit or all roll back.
        await context.SaveChangesAsync(cancellationToken);

        return Result<SubmitOfficialExamResponse>.Success(new(
            ticket.Id,
            ticket.Status,
            now,
            answers.Count));
    }
}
