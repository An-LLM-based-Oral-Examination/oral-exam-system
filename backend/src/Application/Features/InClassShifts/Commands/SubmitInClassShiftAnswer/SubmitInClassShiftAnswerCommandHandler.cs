using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;

public sealed class SubmitInClassShiftAnswerCommandHandler(
    IApplicationDbContext context,
    IGradingQueueChannel gradingQueueChannel,
    IValidator<SubmitInClassShiftAnswerCommand> validator)
    : IRequestHandler<SubmitInClassShiftAnswerCommand, Result<SubmitInClassAnswerResponse>>
{
    public async Task<Result<SubmitInClassAnswerResponse>> Handle(
        SubmitInClassShiftAnswerCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<SubmitInClassAnswerResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var outcome = await context.ExecuteInClassSubmissionAsync(
            request.ShiftId,
            request.StudentId,
            ct => HandleSerialized(request, ct),
            cancellationToken);

        // External queue writes must not run inside the retryable database transaction.
        if (outcome.ExpiredParticipant is not null)
        {
            await InClassShiftAutoSubmit.FinalizeParticipantAsync(
                context,
                gradingQueueChannel,
                outcome.ExpiredParticipant,
                cancellationToken);
            return outcome.Result;
        }

        if (outcome.SubmitMissingForClosedShift)
        {
            await InClassShiftAutoSubmit.SubmitMissingAsync(
                context,
                gradingQueueChannel,
                cancellationToken,
                request.ShiftId,
                respectClosedGrace: true);
        }

        if (outcome.GradingTask is not null)
        {
            await gradingQueueChannel.EnqueueAsync(outcome.GradingTask, cancellationToken);
        }

        return outcome.Result;
    }

    private async Task<SubmissionMutationOutcome> HandleSerialized(
        SubmitInClassShiftAnswerCommand request,
        CancellationToken cancellationToken)
    {
        var shift = await context.InClassExamShifts
            .Include(s => s.Class)
                .ThenInclude(c => c.Course)
            .Include(s => s.Class)
                .ThenInclude(c => c.CourseOffering)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Failure(InClassShiftErrors.ShiftNotFound);
        }

        if (!InClassShiftAcademicContext.IsOperational(shift.Class))
        {
            return Failure(InClassShiftErrors.ClassNotFound);
        }

        var utcNow = DateTime.UtcNow;
        if (InClassShiftLifecycle.TryCloseIfDue(shift, utcNow))
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        var closed = shift.Status is "CLOSED" or "ENDED";
        if (shift.IsLocked || shift.Status == "FINALIZED")
        {
            return Failure(InClassShiftErrors.ShiftLocked);
        }

        if (closed && !InClassShiftLifecycle.CanAcceptLateAnswer(shift, utcNow))
        {
            return Failure(InClassShiftErrors.ShiftClosed);
        }

        if (!closed && shift.Status != "IN_PROGRESS")
        {
            return Failure(InClassShiftErrors.ShiftNotOpen);
        }

        var participant = await context.InClassShiftParticipants
            .FirstOrDefaultAsync(
                p => p.ShiftId == shift.Id && p.StudentId == request.StudentId,
                cancellationToken);

        if (participant is null)
        {
            return Failure(InClassShiftErrors.ParticipantNotFound);
        }

        // Khóa vĩnh viễn sau POST /finish hoặc khi participant đã ở trạng thái kết thúc/vắng mặt.
        if (participant.FinishedAt.HasValue ||
            string.Equals(participant.Status, "GRADED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(participant.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(participant.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(participant.Status, "ABSENT", StringComparison.OrdinalIgnoreCase))
        {
            return Failure($"CONFLICT: {InClassShiftErrors.AlreadyParticipated}");
        }

        // Kẹp hạn chót cá nhân theo Min(JoinedAt + ExamDurationMinutes, shift.EndTime) + ClosedAnswerGrace (2 phút).
        if (InClassShiftLifecycle.IsPersonalExamAnswerWindowExpired(participant, shift, utcNow))
        {
            return new SubmissionMutationOutcome(
                Result<SubmitInClassAnswerResponse>.Failure(InClassShiftErrors.ExamTimeExpired),
                ExpiredParticipant: participant);
        }

        if (!request.IsFollowUp && (request.ParentSubmissionId.HasValue || request.FollowUpIndex != 0))
        {
            return Failure(InClassShiftErrors.InvalidFollowUpRequest);
        }

        if (request.IsFollowUp && (!shift.HasFollowUp || shift.MaxFollowUpQuestions <= 0))
        {
            return Failure(InClassShiftErrors.FollowUpDisabled);
        }

        if (request.IsFollowUp &&
            (request.FollowUpIndex < 1 || request.FollowUpIndex > shift.MaxFollowUpQuestions))
        {
            return Failure(InClassShiftErrors.InvalidFollowUpIndex);
        }

        InClassExamSubmission? followUpParent = null;
        if (request.IsFollowUp)
        {
            if (!request.ParentSubmissionId.HasValue)
            {
                return Failure(InClassShiftErrors.InvalidFollowUpParent);
            }

            followUpParent = await context.InClassExamSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Id == request.ParentSubmissionId.Value &&
                    s.ShiftId == shift.Id &&
                    s.StudentId == request.StudentId &&
                    s.QuestionId == request.QuestionId,
                    cancellationToken);

            if (followUpParent is null ||
                followUpParent.FollowUpIndex != request.FollowUpIndex - 1 ||
                string.IsNullOrWhiteSpace(followUpParent.FollowUpPrompt))
            {
                return Failure(InClassShiftErrors.InvalidFollowUpParent);
            }
        }

        int effectiveFollowUpIndex = request.IsFollowUp ? request.FollowUpIndex : 0;

        // 1. Chặn nộp trùng lặp cùng 1 câu hỏi / lượt follow-up trong cùng 1 ca thi
        var existingSubmission = await context.InClassExamSubmissions
            .FirstOrDefaultAsync(
                s => s.ShiftId == shift.Id &&
                     s.StudentId == request.StudentId &&
                     s.QuestionId == request.QuestionId &&
                     s.FollowUpIndex == effectiveFollowUpIndex,
                cancellationToken);

        var normalizedTranscript = request.TranscriptText.Trim();
        if (existingSubmission is not null)
        {
            // Tuyệt đối không cho phép ghi đè bài nộp trống (Transcript == "") sinh ra từ POST /finish hoặc auto-submit.
            if (string.IsNullOrWhiteSpace(existingSubmission.Transcript))
            {
                return Failure($"CONFLICT: {InClassShiftErrors.AlreadyParticipated}");
            }

            if (string.Equals(existingSubmission.Transcript, normalizedTranscript, StringComparison.Ordinal))
            {
                return Success(new SubmitInClassAnswerResponse(
                    existingSubmission.Id,
                    existingSubmission.ShiftId,
                    existingSubmission.StudentId,
                    existingSubmission.QuestionId,
                    existingSubmission.Status,
                    existingSubmission.SubmittedAt,
                    existingSubmission.IsFollowUp,
                    existingSubmission.ParentSubmissionId,
                    existingSubmission.FollowUpIndex));
            }

            return Failure(InClassShiftErrors.DuplicateSubmission);
        }

        if (string.Equals(participant.Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase) && !request.IsFollowUp)
        {
            return Failure($"CONFLICT: {InClassShiftErrors.AlreadyParticipated}");
        }

        // 2. Nếu hệ thống đã phân bổ câu hỏi cho sinh viên khi vào ca thi, bắt buộc phải nộp đúng trong tập câu hỏi được giao
        var assignedQuestionIds = EnterInClassShiftCommandHandler.ParseGuidList(participant.AssignedQuestionIdsJson);
        if (assignedQuestionIds.Count == 0 && participant.AssignedQuestionId.HasValue)
        {
            assignedQuestionIds.Add(participant.AssignedQuestionId.Value);
        }

        if (assignedQuestionIds.Count > 0 && !assignedQuestionIds.Contains(request.QuestionId))
        {
            return Failure(InClassShiftErrors.QuestionMismatch);
        }

        // 3. Kiểm tra câu hỏi nộp lên bắt buộc phải tồn tại, đã được duyệt (APPROVED) trong PracticeQuestions của môn học (cô lập R8)
        var isValidQuestion = await context.PracticeQuestions
            .AsNoTracking()
            .AnyAsync(q => q.Id == request.QuestionId &&
                           q.CourseId == shift.Class.CourseId &&
                           q.ApprovalStatus == ApprovalStatus.Approved &&
                           q.IsActive &&
                           !q.IsDeleted,
                cancellationToken);

        if (!isValidQuestion)
        {
            return Failure(InClassShiftErrors.InvalidCourseQuestion);
        }

        // Cam kết Zero-R2 Invariant: Tuyệt đối KHÔNG lưu file ghi âm R2, chỉ lưu TranscriptText (Persist-First < 100ms)
        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = request.StudentId,
            QuestionId = request.QuestionId,
            IsFollowUp = request.IsFollowUp,
            ParentSubmissionId = request.ParentSubmissionId,
            FollowUpIndex = effectiveFollowUpIndex,
            Transcript = normalizedTranscript,
            Status = "SUBMITTED",
            SubmittedAt = utcNow
        };

        context.InClassExamSubmissions.Add(submission);

        var followUpEnabled = shift.HasFollowUp && shift.MaxFollowUpQuestions > 0;
        var currentTurnRequiresFollowUp = followUpEnabled && effectiveFollowUpIndex < shift.MaxFollowUpQuestions;

        if (assignedQuestionIds.Count <= 1)
        {
            participant.Status = currentTurnRequiresFollowUp ? "IN_PROGRESS" : "SUBMITTED";
        }
        else
        {
            var existingStudentSubs = await context.InClassExamSubmissions
                .AsNoTracking()
                .Where(s => s.ShiftId == shift.Id && s.StudentId == request.StudentId)
                .Select(s => new
                {
                    s.QuestionId,
                    s.IsFollowUp,
                    s.FollowUpIndex,
                    s.Status,
                    s.FollowUpPrompt
                })
                .ToListAsync(cancellationToken);

            var allTurns = existingStudentSubs
                .Append(new
                {
                    submission.QuestionId,
                    submission.IsFollowUp,
                    submission.FollowUpIndex,
                    submission.Status,
                    submission.FollowUpPrompt
                })
                .ToList();

            var submittedMainIds = allTurns
                .Where(s => !s.IsFollowUp)
                .Select(s => s.QuestionId)
                .ToHashSet();

            var allMainSubmitted = assignedQuestionIds.All(submittedMainIds.Contains);

            var anyQuestionHasPendingFollowUpWork = false;
            if (followUpEnabled && allMainSubmitted)
            {
                var turnsByQuestion = allTurns.GroupBy(s => s.QuestionId).ToDictionary(g => g.Key, g => g.ToList());
                foreach (var qid in assignedQuestionIds)
                {
                    if (!turnsByQuestion.TryGetValue(qid, out var qTurns) || qTurns.Count == 0)
                    {
                        anyQuestionHasPendingFollowUpWork = true;
                        break;
                    }

                    var latestTurn = qTurns.OrderByDescending(t => t.FollowUpIndex).First();
                    if (latestTurn.FollowUpIndex >= shift.MaxFollowUpQuestions)
                    {
                        continue;
                    }

                    var isFinishedWithNoFollowUp =
                        !string.Equals(latestTurn.Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase) &&
                        string.IsNullOrWhiteSpace(latestTurn.FollowUpPrompt);
                    if (!isFinishedWithNoFollowUp)
                    {
                        anyQuestionHasPendingFollowUpWork = true;
                        break;
                    }
                }
            }

            if (allMainSubmitted && !currentTurnRequiresFollowUp && !anyQuestionHasPendingFollowUpWork)
            {
                participant.Status = "SUBMITTED";
            }
            else
            {
                participant.Status = "IN_PROGRESS";
            }
        }

        await context.SaveChangesAsync(cancellationToken);


        // Đẩy vào hàng đợi BoundedChannel (1,000 slots RAM) để GradingQueueWorker chấm ngầm bất đồng bộ
        var gradingTask = new GradingTask
        {
            AnswerId = submission.Id,
            SessionId = shift.Id,
            StudentId = request.StudentId,
            QuestionId = request.QuestionId,
            AnswerText = submission.Transcript,
            IsFollowUp = request.IsFollowUp,
            ParentAnswerId = request.ParentSubmissionId,
            TaskType = "InClass",
            InClassSubmissionId = submission.Id
        };

        var response = new SubmitInClassAnswerResponse(
            submission.Id,
            shift.Id,
            request.StudentId,
            request.QuestionId,
            submission.Status,
            submission.SubmittedAt,
            submission.IsFollowUp,
            submission.ParentSubmissionId,
            submission.FollowUpIndex
        );

        return new SubmissionMutationOutcome(
            Result<SubmitInClassAnswerResponse>.Success(response),
            GradingTask: gradingTask,
            SubmitMissingForClosedShift: closed);
    }

    private static SubmissionMutationOutcome Failure(string error) =>
        new(Result<SubmitInClassAnswerResponse>.Failure(error));

    private static SubmissionMutationOutcome Success(SubmitInClassAnswerResponse response) =>
        new(Result<SubmitInClassAnswerResponse>.Success(response));

    private sealed record SubmissionMutationOutcome(
        Result<SubmitInClassAnswerResponse> Result,
        GradingTask? GradingTask = null,
        bool SubmitMissingForClosedShift = false,
        InClassShiftParticipant? ExpiredParticipant = null);
}
