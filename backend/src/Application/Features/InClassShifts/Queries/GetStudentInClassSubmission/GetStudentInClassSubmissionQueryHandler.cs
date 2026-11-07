using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetStudentInClassSubmission;

public sealed class GetStudentInClassSubmissionQueryHandler(
    IApplicationDbContext context)
    : IRequestHandler<GetStudentInClassSubmissionQuery, Result<InClassExamSubmissionResponse>>
{
    public async Task<Result<InClassExamSubmissionResponse>> Handle(
        GetStudentInClassSubmissionQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.InClassExamSubmissions
            .AsNoTracking()
            .Include(s => s.Shift)
            .Include(s => s.Student)
            .Where(s => s.ShiftId == request.ShiftId && s.StudentId == request.StudentId);

        if (request.QuestionId.HasValue && request.QuestionId.Value != Guid.Empty)
        {
            query = query.Where(s => s.QuestionId == request.QuestionId.Value);
        }

        if (request.FollowUpIndex.HasValue)
        {
            query = query.Where(s => s.FollowUpIndex == request.FollowUpIndex.Value);
        }

        var submission = await query
            .OrderByDescending(s => s.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (submission is null)
        {
            return Result<InClassExamSubmissionResponse>.Failure(InClassShiftErrors.SubmissionNotFound);
        }

        var qInfo = await context.PracticeQuestions
            .AsNoTracking()
            .Where(q => q.Id == submission.QuestionId)
            .Select(q => new { q.Title, q.Content })
            .FirstOrDefaultAsync(cancellationToken);

        bool canViewScores = submission.Shift is not null &&
            (submission.Shift.IsLocked || submission.Shift.Status == "FINALIZED" || submission.Shift.IsAutoPublishGrades);
        bool canViewLecturerNotes = submission.Shift is not null &&
            (submission.Shift.IsLocked || submission.Shift.Status == "FINALIZED");

        var response = new InClassExamSubmissionResponse(
            submission.Id,
            submission.ShiftId,
            submission.StudentId,
            submission.Student?.FullName,
            submission.Student?.Email,
            submission.QuestionId,
            submission.Transcript,
            canViewScores ? submission.AiScore : null,
            canViewScores ? submission.AiFeedback : null,
            canViewScores ? submission.FinalScore : null,
            canViewLecturerNotes ? submission.LecturerNotes : null,
            submission.Status,
            submission.SubmittedAt,
            submission.GradedAt,
            qInfo?.Title,
            qInfo?.Content,
            submission.IsFollowUp,
            submission.ParentSubmissionId,
            submission.FollowUpIndex,
            submission.FollowUpPrompt,
            canViewScores ? submission.AiConfidenceScore : null,
            submission.Student?.StudentCode
        );

        return Result<InClassExamSubmissionResponse>.Success(response);
    }
}
