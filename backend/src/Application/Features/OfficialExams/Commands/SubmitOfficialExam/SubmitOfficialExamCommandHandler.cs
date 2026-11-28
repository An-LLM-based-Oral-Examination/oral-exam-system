using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
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
        return await context.ExecuteTicketDecisionAsync(
            request.TicketId,
            ct => HandleLockedAsync(request, ct),
            cancellationToken);
    }

    private async Task<Result<SubmitOfficialExamResponse>> HandleLockedAsync(
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

        var student = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == ticket.StudentId, cancellationToken);

        if (ticket.StudentId != request.ActorUserId ||
            (student is not null && (student.Role != UserRole.Student || !student.IsActive)))
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var deviceError = OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            ticket, request.DeviceId, request.KioskSessionToken, requireSessionToken: true, requireBoundDevice: true);
        if (deviceError is not null)
        {
            return Result<SubmitOfficialExamResponse>.Failure(deviceError);
        }

        if (ticket.SubmittedAt.HasValue && ticket.Submissions.Count > 0)
        {
            return Result<SubmitOfficialExamResponse>.Success(new SubmitOfficialExamResponse(
                ticket.Id,
                ticket.Status,
                ticket.SubmittedAt.Value,
                ticket.Submissions.Count,
                IsKioskLocked: true));
        }

        if (ticket.Status == ExamTicketStatus.Locked || ticket.IsLocked)
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.TicketLocked);
        }

        if (ticket.Status is ExamTicketStatus.Submitted or ExamTicketStatus.AiGraded or ExamTicketStatus.Audited or ExamTicketStatus.Published)
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.SubmissionAlreadyExists);
        }

        if (!string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.TicketNotInProgress);
        }

        var shift = await context.RealExamSessionShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == ticket.ShiftId, cancellationToken);
        if (shift is not null)
        {
            var shiftWindowError = ExamShiftWindow.ValidateActiveSubmissionWindow(shift, DateTime.UtcNow);
            if (shiftWindowError is not null)
            {
                return Result<SubmitOfficialExamResponse>.Failure(shiftWindowError);
            }
        }

        if (!ticket.ExamSetId.HasValue)
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.ExamSetNotAssigned);
        }

        var expectedQuestionIds = await context.ExamSetQuestions
            .AsNoTracking()
            .Where(x => x.ExamSetId == ticket.ExamSetId.Value)
            .OrderBy(x => x.OrderIndex)
            .Select(x => x.ExamQuestionId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        if (request.FinalizePerTurn && request.Answers is { Count: > 0 })
        {
            return Result<SubmitOfficialExamResponse>.Failure(
                "BAD_REQUEST: Chế độ chốt bài theo từng lượt (finalizePerTurn = true) không chấp nhận danh sách answers nộp gộp.");
        }

        // Per-turn finalize mode: student already submitted main/follow-up turns via PUT .../questions/{id}/answers
        // hoặc Desktop Kiosk chốt bài tường minh theo chế độ per-turn (kể cả ca biên 0 câu khi hết giờ/nộp trắng bài).
        var hasPerTurnSubmissions = ticket.Submissions.Any(s => !s.IsFollowUp);
        var isExplicitPerTurnFinalize = request.FinalizePerTurn
            && (request.Answers is null || request.Answers.Count == 0);
        if (hasPerTurnSubmissions || isExplicitPerTurnFinalize)
        {
            if (expectedQuestionIds.Count == 0)
            {
                return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.ExamSetNoQuestions);
            }

            var answeredMainIds = ticket.Submissions
                .Where(s => !s.IsFollowUp)
                .Select(s => s.ExamQuestionId)
                .ToHashSet();

            var missingMainIds = expectedQuestionIds
                .Where(qId => !answeredMainIds.Contains(qId))
                .ToList();

            var existingSubmissionCount = ticket.Submissions.Count;
            foreach (var missingQuestionId in missingMainIds)
            {
                var emptySubmission = new ExamQuestionSubmission
                {
                    Id = Guid.NewGuid(),
                    TicketId = ticket.Id,
                    ExamQuestionId = missingQuestionId,
                    IsFollowUp = false,
                    FollowUpIndex = 0,
                    TranscriptWhisper = string.Empty,
                    TimeSpentSeconds = 0,
                    GradingStatus = "pending",
                    SubmittedAt = now
                };

                context.ExamQuestionSubmissions.Add(emptySubmission);
                context.GradingJobs.Add(new GradingJob
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = emptySubmission.Id,
                    Status = "pending",
                    AttemptCount = 0,
                    NextAttemptAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            // Mở khóa ngay các GradingJob từng lượt đang tạm hoãn chờ follow-up vì thí sinh đã chốt nộp toàn bộ bài thi.
            var existingSubmissionIds = ticket.Submissions.Select(s => s.Id).ToList();
            if (existingSubmissionIds.Count > 0)
            {
                var deferredJobs = await context.GradingJobs
                    .Where(j => existingSubmissionIds.Contains(j.SubmissionId)
                        && j.Status == "pending"
                        && j.NextAttemptAt != null
                        && j.NextAttemptAt > now)
                    .ToListAsync(cancellationToken);

                foreach (var deferredJob in deferredJobs)
                {
                    deferredJob.NextAttemptAt = now;
                    deferredJob.UpdatedAt = now;
                }
            }

            var allAlreadyGraded = missingMainIds.Count == 0
                && existingSubmissionCount > 0
                && ticket.Submissions.All(s =>
                    string.Equals(s.GradingStatus, "ai_graded", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.GradingStatus, "audited", StringComparison.OrdinalIgnoreCase));

            // Chỉ chuyển từ IN_PROGRESS sang AI_GRADED/SUBMITTED bên trong khóa ExecuteTicketDecisionAsync,
            // tuyệt đối không hạ ngược trạng thái nếu worker đã thăng hạng vé sang AI_GRADED/AUDITED.
            if (string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.OrdinalIgnoreCase))
            {
                ticket.Status = allAlreadyGraded ? ExamTicketStatus.AiGraded : ExamTicketStatus.Submitted;
            }
            ticket.SubmittedAt ??= now;
            await context.SaveChangesAsync(cancellationToken);

            return Result<SubmitOfficialExamResponse>.Success(new SubmitOfficialExamResponse(
                ticket.Id,
                ticket.Status,
                ticket.SubmittedAt.Value,
                existingSubmissionCount + missingMainIds.Count,
                IsKioskLocked: true));
        }

        // Legacy / non-follow-up batch submission mode (master audio STT_MSSV.webm)
        if (!ticket.AudioVerifiedAt.HasValue ||
            string.IsNullOrWhiteSpace(ticket.AudioObjectKey) ||
            string.IsNullOrWhiteSpace(ticket.AudioHashSha256))
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.AudioNotSealed);
        }

        var answers = request.Answers;
        if (answers is null || answers.Count == 0)
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.SubmissionQuestionsMismatch);
        }

        var duplicateQuestionIds = answers
            .GroupBy(x => x.ExamQuestionId)
            .Any(group => group.Count() > 1);
        if (duplicateQuestionIds)
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.SubmissionQuestionsMismatch);
        }

        var submittedQuestionIds = answers.Select(x => x.ExamQuestionId).ToHashSet();
        if (expectedQuestionIds.Count == 0 ||
            expectedQuestionIds.Count != submittedQuestionIds.Count ||
            expectedQuestionIds.Any(id => !submittedQuestionIds.Contains(id)))
        {
            return Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.SubmissionQuestionsMismatch);
        }

        var shiftSessionId = await context.RealExamSessionShifts
            .AsNoTracking()
            .Where(sh => sh.Id == ticket.ShiftId)
            .Select(sh => (Guid?)sh.SessionId)
            .FirstOrDefaultAsync(cancellationToken);

        var examInputMode = shiftSessionId.HasValue
            ? await context.OfficialExamSessions
                .AsNoTracking()
                .Where(s => s.Id == shiftSessionId.Value)
                .Select(s => s.ExamInputMode)
                .FirstOrDefaultAsync(cancellationToken) ?? ExamInputMode.VoiceWithTranscriptEdit
            : ExamInputMode.VoiceWithTranscriptEdit;

        var computedRatios = new List<decimal?>(answers.Count);
        foreach (var answer in answers)
        {
            var editCheck = TranscriptEditRatioCalculator.ValidateAndCompute(
                examInputMode,
                answer.InitialVoiceTranscript,
                answer.TranscriptWhisper,
                answer.IsManuallyEdited);
            if (!editCheck.IsSuccess)
            {
                return Result<SubmitOfficialExamResponse>.Failure(editCheck.Error);
            }

            computedRatios.Add(editCheck.Value);
        }

        var isVoiceOnly = string.Equals(examInputMode, ExamInputMode.VoiceOnly, StringComparison.OrdinalIgnoreCase);

        for (var i = 0; i < answers.Count; i++)
        {
            var answer = answers[i];
            var effectiveTranscript = isVoiceOnly
                ? (answer.InitialVoiceTranscript ?? answer.TranscriptWhisper ?? string.Empty).Trim()
                : (answer.TranscriptWhisper ?? string.Empty).Trim();

            var submission = new ExamQuestionSubmission
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                ExamQuestionId = answer.ExamQuestionId,
                IsFollowUp = false,
                FollowUpIndex = 0,
                AudioObjectKey = ticket.AudioObjectKey,
                AudioR2Url = ticket.AudioObjectKey,
                AudioHashSha256 = ticket.AudioHashSha256,
                AudioVerifiedAt = ticket.AudioVerifiedAt,
                InitialVoiceTranscript = answer.InitialVoiceTranscript is null ? null : answer.InitialVoiceTranscript.Trim(),
                TranscriptWhisper = effectiveTranscript,
                TranscriptEditRatio = computedRatios[i],
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

        return Result<SubmitOfficialExamResponse>.Success(new SubmitOfficialExamResponse(
            ticket.Id,
            ticket.Status,
            now,
            answers.Count,
            IsKioskLocked: true));
    }
}
