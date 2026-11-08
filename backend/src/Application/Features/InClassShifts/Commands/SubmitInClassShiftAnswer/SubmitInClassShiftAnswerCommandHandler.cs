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

        var shift = await context.InClassExamShifts
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<SubmitInClassAnswerResponse>.Failure(InClassShiftErrors.ShiftNotFound);
        }

        if (InClassShiftLifecycle.TryCloseIfDue(shift, DateTime.UtcNow))
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        var closed = shift.Status is "CLOSED" or "ENDED";
        if (shift.IsLocked || shift.Status == "FINALIZED")
        {
            return Result<SubmitInClassAnswerResponse>.Failure(InClassShiftErrors.ShiftLocked);
        }

        if (closed && !InClassShiftLifecycle.CanAcceptLateAnswer(shift, DateTime.UtcNow))
        {
            return Result<SubmitInClassAnswerResponse>.Failure(InClassShiftErrors.ShiftClosed);
        }

        if (!closed && shift.Status != "IN_PROGRESS")
        {
            return Result<SubmitInClassAnswerResponse>.Failure(InClassShiftErrors.ShiftNotOpen);
        }

        var participant = await context.InClassShiftParticipants
            .FirstOrDefaultAsync(
                p => p.ShiftId == shift.Id && p.StudentId == request.StudentId,
                cancellationToken);

        if (participant is null)
        {
            return Result<SubmitInClassAnswerResponse>.Failure(InClassShiftErrors.ParticipantNotFound);
        }

        // Cho phép follow-up sau khi đã nộp đủ câu chính (Status có thể đã SUBMITTED).
        // Chỉ khóa khi hết thời gian làm bài cá nhân.
        if (InClassShiftLifecycle.IsPersonalExamTimeExpired(participant, shift, DateTime.UtcNow))
        {
            await InClassShiftAutoSubmit.FinalizeParticipantAsync(
                context,
                gradingQueueChannel,
                participant,
                cancellationToken);
            return Result<SubmitInClassAnswerResponse>.Failure(InClassShiftErrors.ExamTimeExpired);
        }

        int effectiveFollowUpIndex = request.IsFollowUp ? Math.Max(1, request.FollowUpIndex) : 0;

        // 1. Chặn nộp trùng lặp cùng 1 câu hỏi / lượt follow-up trong cùng 1 ca thi
        var existingSubmission = await context.InClassExamSubmissions
            .FirstOrDefaultAsync(
                s => s.ShiftId == shift.Id &&
                     s.StudentId == request.StudentId &&
                     s.QuestionId == request.QuestionId &&
                     s.FollowUpIndex == effectiveFollowUpIndex,
                cancellationToken);

        if (existingSubmission is not null && !string.IsNullOrWhiteSpace(existingSubmission.Transcript))
        {
            return Result<SubmitInClassAnswerResponse>.Failure(InClassShiftErrors.DuplicateSubmission);
        }

        // 2. Nếu hệ thống đã phân bổ câu hỏi cho sinh viên khi vào ca thi, bắt buộc phải nộp đúng trong tập câu hỏi được giao
        var assignedQuestionIds = EnterInClassShiftCommandHandler.ParseGuidList(participant.AssignedQuestionIdsJson);
        if (assignedQuestionIds.Count == 0 && participant.AssignedQuestionId.HasValue)
        {
            assignedQuestionIds.Add(participant.AssignedQuestionId.Value);
        }

        if (assignedQuestionIds.Count > 0 && !assignedQuestionIds.Contains(request.QuestionId))
        {
            return Result<SubmitInClassAnswerResponse>.Failure(InClassShiftErrors.QuestionMismatch);
        }

        // 3. Kiểm tra câu hỏi nộp lên bắt buộc phải tồn tại, đã được duyệt (APPROVED) trong PracticeQuestions của môn học (cô lập R8)
        var questionInfo = await context.PracticeQuestions
            .AsNoTracking()
            .Where(q => q.Id == request.QuestionId &&
                        q.CourseId == shift.Class.CourseId &&
                        q.ApprovalStatus == ApprovalStatus.Approved &&
                        q.IsActive)
            .Select(q => new { q.Content, q.SampleAnswer, q.KeyPoints })
            .FirstOrDefaultAsync(cancellationToken);

        if (questionInfo is null)
        {
            return Result<SubmitInClassAnswerResponse>.Failure(InClassShiftErrors.InvalidCourseQuestion);
        }

        string questionContent = questionInfo.Content;
        string sampleAnswer = questionInfo.SampleAnswer ?? string.Empty;
        string keyPoints = questionInfo.KeyPoints ?? "[]";

        DateTime utcNow = DateTime.UtcNow;
        InClassExamSubmission submission;

        if (existingSubmission is not null)
        {
            existingSubmission.Transcript = request.TranscriptText.Trim();
            existingSubmission.Status = "SUBMITTED";
            existingSubmission.SubmittedAt = utcNow;
            existingSubmission.AiScore = null;
            existingSubmission.AiConfidenceScore = null;
            existingSubmission.AiFeedback = null;
            existingSubmission.CotTrace = null;
            existingSubmission.FinalScore = null;
            existingSubmission.GradedAt = null;
            submission = existingSubmission;
        }
        else
        {
            // Cam kết Zero-R2 Invariant: Tuyệt đối KHÔNG lưu file ghi âm R2, chỉ lưu TranscriptText (Persist-First < 100ms)
            submission = new InClassExamSubmission
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = request.StudentId,
                QuestionId = request.QuestionId,
                IsFollowUp = request.IsFollowUp,
                ParentSubmissionId = request.ParentSubmissionId,
                FollowUpIndex = effectiveFollowUpIndex,
                Transcript = request.TranscriptText.Trim(),
                Status = "SUBMITTED",
                SubmittedAt = utcNow
            };

            context.InClassExamSubmissions.Add(submission);

            bool requiresMoreFollowUp = shift.HasFollowUp &&
                shift.MaxFollowUpQuestions > 0 &&
                effectiveFollowUpIndex < shift.MaxFollowUpQuestions;

            if (assignedQuestionIds.Count <= 1)
            {
                participant.Status = requiresMoreFollowUp ? "IN_PROGRESS" : "SUBMITTED";
            }
            else
            {
                var submittedMainIds = await context.InClassExamSubmissions
                    .AsNoTracking()
                    .Where(s => s.ShiftId == shift.Id && s.StudentId == request.StudentId && !s.IsFollowUp)
                    .Select(s => s.QuestionId)
                    .ToListAsync(cancellationToken);

                if (!request.IsFollowUp)
                {
                    submittedMainIds.Add(request.QuestionId);
                }

                if (assignedQuestionIds.All(id => submittedMainIds.Contains(id)) && !requiresMoreFollowUp)
                {
                    participant.Status = "SUBMITTED";
                }
                else
                {
                    participant.Status = "IN_PROGRESS";
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        if (closed)
        {
            await InClassShiftAutoSubmit.SubmitMissingAsync(context, gradingQueueChannel, cancellationToken, shift.Id);
        }

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

        await gradingQueueChannel.EnqueueAsync(gradingTask, cancellationToken);

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

        return Result<SubmitInClassAnswerResponse>.Success(response);
    }
}
