using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialQuestionAnswer;

public sealed partial class SubmitOfficialQuestionAnswerCommandHandler(
    IApplicationDbContext context,
    IStorageService storageService)
    : IRequestHandler<SubmitOfficialQuestionAnswerCommand, Result<SubmitOfficialQuestionAnswerResponse>>
{
    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256HexRegex();

    public async Task<Result<SubmitOfficialQuestionAnswerResponse>> Handle(
        SubmitOfficialQuestionAnswerCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FollowUpIndex < 0 || request.FollowUpIndex > OfficialExamRules.MaxFollowUpQuestions)
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(
                $"UNPROCESSABLE: Chỉ số lượt câu hỏi phụ (followUpIndex) phải từ 0 đến {OfficialExamRules.MaxFollowUpQuestions}.");
        }

        if (request.TimeSpentSeconds < 0 || request.TimeSpentSeconds > 14_400)
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(
                "UNPROCESSABLE: Thời gian trả lời (timeSpentSeconds) phải từ 0 đến 14400 giây.");
        }

        if (string.IsNullOrWhiteSpace(request.AudioObjectKey))
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(
                "UNPROCESSABLE: Đường dẫn audio (audioObjectKey) không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.AudioHashSha256) || !Sha256HexRegex().IsMatch(request.AudioHashSha256.Trim()))
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(
                "UNPROCESSABLE: Mã SHA-256 phải là chuỗi hex 64 ký tự hợp lệ.");
        }

        var normalizedSha256 = request.AudioHashSha256.Trim().ToLowerInvariant();
        var normalizedObjectKey = request.AudioObjectKey.Trim();

        // Pha 1 (NGOÀI transaction / lock DB): đọc AsNoTracking để xác thực nhanh, xử lý idempotency
        // và kiểm tra object R2 + SHA-256 mà KHÔNG BAO GIỜ giam PostgreSQL connection/transaction trong suốt HTTP I/O.
        var preTicket = await context.StudentExamTickets
            .AsNoTracking()
            .Include(t => t.Student)
            .Include(t => t.Shift)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (preTicket is null)
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.TicketNotFound);
        }

        if (preTicket.StudentId != request.ActorUserId || preTicket.Student.Role != UserRole.Student || !preTicket.Student.IsActive)
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var preDeviceError = OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            preTicket, request.DeviceId, request.KioskSessionToken, requireSessionToken: true, requireBoundDevice: true);
        if (preDeviceError is not null)
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(preDeviceError);
        }

        if (preTicket.Status == ExamTicketStatus.Locked || preTicket.IsLocked)
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.TicketLocked);
        }

        if (!string.Equals(preTicket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.TicketNotInProgress);
        }

        if (preTicket.Shift is null)
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure("CONFLICT: Ca thi không tồn tại hoặc chưa mở phát đề.");
        }

        var preShiftWindowError = ExamShiftWindow.ValidateActiveSubmissionWindow(preTicket.Shift, DateTime.UtcNow);
        if (preShiftWindowError is not null)
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(preShiftWindowError);
        }

        if (!preTicket.ExamSetId.HasValue)
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.ExamSetNotAssigned);
        }

        if (string.IsNullOrWhiteSpace(preTicket.Student.StudentCode))
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.StudentNotFound);
        }

        var examSetQuestion = await context.ExamSetQuestions
            .AsNoTracking()
            .Where(esq => esq.ExamSetId == preTicket.ExamSetId.Value && esq.ExamQuestionId == request.QuestionId)
            .Select(esq => new { esq.OrderIndex })
            .FirstOrDefaultAsync(cancellationToken);

        if (examSetQuestion is null)
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.SubmissionQuestionsMismatch);
        }

        var expectedObjectKey = OfficialExamAudioObjectKey.CreateForQuestion(
            preTicket.ShiftId,
            preTicket.Id,
            preTicket.SeatNumber,
            preTicket.Student.StudentCode,
            examSetQuestion.OrderIndex,
            request.FollowUpIndex);

        if (!string.Equals(expectedObjectKey, normalizedObjectKey, StringComparison.Ordinal))
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.AudioObjectKeyMismatch);
        }

        var preSession = await context.OfficialExamSessions
            .AsNoTracking()
            .Where(s => s.Id == preTicket.Shift.SessionId)
            .Select(s => new { s.ExamInputMode, s.HasFollowUp, s.MaxFollowUpQuestions })
            .FirstOrDefaultAsync(cancellationToken);

        var preExamInputMode = preSession?.ExamInputMode ?? ExamInputMode.VoiceWithTranscriptEdit;
        var preHasFollowUp = preSession?.HasFollowUp ?? false;
        var preMaxFollowUp = preSession?.MaxFollowUpQuestions ?? OfficialExamRules.DefaultFollowUpQuestions;

        // Idempotency fast-path trước khi gọi R2 I/O
        var preExisting = await context.ExamQuestionSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.TicketId == preTicket.Id
                    && s.ExamQuestionId == request.QuestionId
                    && s.FollowUpIndex == request.FollowUpIndex,
                cancellationToken);

        if (preExisting is not null)
        {
            if (string.Equals(preExisting.AudioObjectKey ?? preExisting.AudioR2Url, expectedObjectKey, StringComparison.Ordinal)
                && string.Equals(preExisting.AudioHashSha256, normalizedSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Result<SubmitOfficialQuestionAnswerResponse>.Success(new SubmitOfficialQuestionAnswerResponse(
                    preExisting.Id,
                    preTicket.Id,
                    request.QuestionId,
                    preExisting.FollowUpIndex,
                    expectedObjectKey,
                    normalizedSha256,
                    "RECORDED",
                    preHasFollowUp && preExisting.FollowUpIndex < preMaxFollowUp));
            }

            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.SubmissionAlreadyExists);
        }

        var isFollowUp = request.FollowUpIndex > 0;
        if (isFollowUp)
        {
            if (!preHasFollowUp || request.FollowUpIndex > preMaxFollowUp)
            {
                return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.FollowUpNotEnabled);
            }

            if (!request.ParentSubmissionId.HasValue || request.ParentSubmissionId.Value == Guid.Empty)
            {
                return Result<SubmitOfficialQuestionAnswerResponse>.Failure(
                    "UNPROCESSABLE: Lượt trả lời câu hỏi phụ bắt buộc có parentSubmissionId hợp lệ.");
            }

            var preParent = await context.ExamQuestionSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.Id == request.ParentSubmissionId.Value
                        && s.TicketId == preTicket.Id
                        && s.ExamQuestionId == request.QuestionId
                        && s.FollowUpIndex == request.FollowUpIndex - 1,
                    cancellationToken);

            if (preParent is null)
            {
                return Result<SubmitOfficialQuestionAnswerResponse>.Failure(
                    "NOT_FOUND: Không tìm thấy bài nộp câu hỏi cha tương ứng cho lượt câu hỏi phụ này.");
            }

            if (string.IsNullOrWhiteSpace(preParent.GeneratedFollowUpPrompt))
            {
                return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.FollowUpParentPromptMissing);
            }
        }

        // Validate VoiceOnly & <= 35% transcript edit cap trước khi gọi R2
        var editCheck = TranscriptEditRatioCalculator.ValidateAndCompute(
            preExamInputMode,
            request.InitialVoiceTranscript,
            request.TranscriptWhisper,
            request.IsManuallyEdited);
        if (!editCheck.IsSuccess)
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(editCheck.Error);
        }

        // Verify audio object metadata + SHA-256 on R2 (O(1) HeadObject fast-path) HOÀN TOÀN NGOÀI PostgreSQL transaction!
        var metadata = await storageService.GetObjectMetadataAsync(expectedObjectKey, cancellationToken);
        if (metadata is null)
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.AudioObjectNotFound);
        }

        if (metadata.ContentLength <= 0
            || metadata.ContentLength > OfficialExamRules.MaxOfficialExamAudioBytes
            || !string.Equals(metadata.ContentType, "audio/webm", StringComparison.OrdinalIgnoreCase))
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.AudioObjectInvalid);
        }

        if (!await storageService.VerifySha256Async(expectedObjectKey, normalizedSha256, cancellationToken))
        {
            return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.AudioHashMismatch);
        }

        // Pha 2 (TRONG ExecuteTicketDecisionAsync): đọc tươi trạng thái vé/ca thi + ghi DB nguyên tử (ZERO external HTTP I/O).
        return await context.ExecuteTicketDecisionAsync(
            request.TicketId,
            async ct =>
            {
                var ticket = await context.StudentExamTickets
                    .Include(t => t.Student)
                    .Include(t => t.Shift)
                    .FirstOrDefaultAsync(t => t.Id == request.TicketId, ct);

                if (ticket is null)
                {
                    return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.TicketNotFound);
                }

                if (ticket.StudentId != request.ActorUserId || ticket.Student.Role != UserRole.Student || !ticket.Student.IsActive)
                {
                    return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.Forbidden);
                }

                var deviceError = OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
                    ticket, request.DeviceId, request.KioskSessionToken, requireSessionToken: true, requireBoundDevice: true);
                if (deviceError is not null)
                {
                    return Result<SubmitOfficialQuestionAnswerResponse>.Failure(deviceError);
                }

                if (ticket.Status == ExamTicketStatus.Locked || ticket.IsLocked)
                {
                    return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.TicketLocked);
                }

                if (!string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
                {
                    return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.TicketNotInProgress);
                }

                if (ticket.Shift is null)
                {
                    return Result<SubmitOfficialQuestionAnswerResponse>.Failure("CONFLICT: Ca thi không tồn tại hoặc chưa mở phát đề.");
                }

                var nowUtc = DateTime.UtcNow;
                var shiftWindowError = ExamShiftWindow.ValidateActiveSubmissionWindow(ticket.Shift, nowUtc);
                if (shiftWindowError is not null)
                {
                    return Result<SubmitOfficialQuestionAnswerResponse>.Failure(shiftWindowError);
                }

                if (!ticket.ExamSetId.HasValue)
                {
                    return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.ExamSetNotAssigned);
                }

                var session = await context.OfficialExamSessions
                    .AsNoTracking()
                    .Where(s => s.Id == ticket.Shift.SessionId)
                    .Select(s => new { s.ExamInputMode, s.HasFollowUp, s.MaxFollowUpQuestions })
                    .FirstOrDefaultAsync(ct);

                var examInputMode = session?.ExamInputMode ?? ExamInputMode.VoiceWithTranscriptEdit;
                var hasFollowUp = session?.HasFollowUp ?? false;
                var maxFollowUp = session?.MaxFollowUpQuestions ?? OfficialExamRules.DefaultFollowUpQuestions;

                // Kiểm tra lại idempotency bên trong lock để chống race condition giữa 2 request song song
                var existing = await context.ExamQuestionSubmissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.TicketId == ticket.Id
                            && s.ExamQuestionId == request.QuestionId
                            && s.FollowUpIndex == request.FollowUpIndex,
                        ct);

                if (existing is not null)
                {
                    if (string.Equals(existing.AudioObjectKey ?? existing.AudioR2Url, expectedObjectKey, StringComparison.Ordinal)
                        && string.Equals(existing.AudioHashSha256, normalizedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        return Result<SubmitOfficialQuestionAnswerResponse>.Success(new SubmitOfficialQuestionAnswerResponse(
                            existing.Id,
                            ticket.Id,
                            request.QuestionId,
                            existing.FollowUpIndex,
                            expectedObjectKey,
                            normalizedSha256,
                            "RECORDED",
                            hasFollowUp && existing.FollowUpIndex < maxFollowUp));
                    }

                    return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.SubmissionAlreadyExists);
                }

                // Re-validate follow-up chain & copy parent.GeneratedFollowUpPrompt -> child.FollowUpPrompt (obeys CHECK 23514)
                Guid? parentSubmissionId = null;
                string? childFollowUpPrompt = null;

                if (isFollowUp)
                {
                    if (!hasFollowUp || request.FollowUpIndex > maxFollowUp)
                    {
                        return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.FollowUpNotEnabled);
                    }

                    var parent = await context.ExamQuestionSubmissions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            s => s.Id == request.ParentSubmissionId!.Value
                                && s.TicketId == ticket.Id
                                && s.ExamQuestionId == request.QuestionId
                                && s.FollowUpIndex == request.FollowUpIndex - 1,
                            ct);

                    if (parent is null)
                    {
                        return Result<SubmitOfficialQuestionAnswerResponse>.Failure(
                            "NOT_FOUND: Không tìm thấy bài nộp câu hỏi cha tương ứng cho lượt câu hỏi phụ này.");
                    }

                    if (string.IsNullOrWhiteSpace(parent.GeneratedFollowUpPrompt))
                    {
                        return Result<SubmitOfficialQuestionAnswerResponse>.Failure(OfficialExamErrors.FollowUpParentPromptMissing);
                    }

                    parentSubmissionId = parent.Id;
                    childFollowUpPrompt = parent.GeneratedFollowUpPrompt.Trim();
                }

                var lockedEditCheck = TranscriptEditRatioCalculator.ValidateAndCompute(
                    examInputMode,
                    request.InitialVoiceTranscript,
                    request.TranscriptWhisper,
                    request.IsManuallyEdited);
                if (!lockedEditCheck.IsSuccess)
                {
                    return Result<SubmitOfficialQuestionAnswerResponse>.Failure(lockedEditCheck.Error);
                }

                var effectiveTranscript = string.Equals(examInputMode, ExamInputMode.VoiceOnly, StringComparison.OrdinalIgnoreCase)
                    ? (request.InitialVoiceTranscript ?? request.TranscriptWhisper ?? string.Empty).Trim()
                    : (request.TranscriptWhisper ?? string.Empty).Trim();

                var submission = new ExamQuestionSubmission
                {
                    Id = Guid.NewGuid(),
                    TicketId = ticket.Id,
                    ExamQuestionId = request.QuestionId,
                    IsFollowUp = isFollowUp,
                    ParentSubmissionId = parentSubmissionId,
                    FollowUpIndex = request.FollowUpIndex,
                    FollowUpPrompt = childFollowUpPrompt,
                    GeneratedFollowUpPrompt = null,
                    InitialVoiceTranscript = request.InitialVoiceTranscript is null ? null : request.InitialVoiceTranscript.Trim(),
                    TranscriptWhisper = effectiveTranscript,
                    TranscriptEditRatio = lockedEditCheck.Value,
                    TimestampsWhisper = string.IsNullOrWhiteSpace(request.TimestampsWhisper) ? null : request.TimestampsWhisper,
                    AudioObjectKey = expectedObjectKey,
                    AudioR2Url = expectedObjectKey,
                    AudioHashSha256 = normalizedSha256,
                    AudioVerifiedAt = nowUtc,
                    TimeSpentSeconds = request.TimeSpentSeconds,
                    GradingStatus = "pending",
                    SubmittedAt = nowUtc
                };

                var canRequestFollowUp = hasFollowUp && submission.FollowUpIndex < maxFollowUp;
                context.ExamQuestionSubmissions.Add(submission);
                context.GradingJobs.Add(new GradingJob
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submission.Id,
                    Status = "pending",
                    AttemptCount = 0,
                    // Trì hoãn ngắn khi câu hỏi có thể sinh follow-up để tránh OfficialExamGradingWorker đua STT/LLM với GenerateOfficialExamFollowUp.
                    // GenerateOfficialExamFollowUp và SubmitOfficialExam sẽ mở khóa ngay (NextAttemptAt = now) khi hoàn tất hoặc chốt bài.
                    NextAttemptAt = canRequestFollowUp
                        ? nowUtc.AddSeconds(OfficialExamRules.FollowUpGradingDeferralSeconds)
                        : nowUtc,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                });

                await context.SaveChangesAsync(ct);

                return Result<SubmitOfficialQuestionAnswerResponse>.Success(new SubmitOfficialQuestionAnswerResponse(
                    submission.Id,
                    ticket.Id,
                    request.QuestionId,
                    submission.FollowUpIndex,
                    expectedObjectKey,
                    normalizedSha256,
                    "RECORDED",
                    canRequestFollowUp));
            },
            cancellationToken);
    }
}
