using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.SealOfficialExamAudio;

public sealed class SealOfficialExamAudioCommandHandler(
    IApplicationDbContext context,
    IStorageService storageService)
    : IRequestHandler<SealOfficialExamAudioCommand, Result<OfficialExamAudioSealResponse>>
{
    public async Task<Result<OfficialExamAudioSealResponse>> Handle(
        SealOfficialExamAudioCommand request,
        CancellationToken cancellationToken)
    {
        // Pha 1 (NGOÀI ExecuteTicketDecisionAsync): đọc AsNoTracking để xác thực nhanh, idempotency
        // và xác minh R2 object metadata + SHA-256 mà KHÔNG BAO GIỜ giữ lock/transaction DB trong suốt HTTP I/O.
        var preTicket = await context.StudentExamTickets
            .AsNoTracking()
            .Include(x => x.Student)
            .FirstOrDefaultAsync(x => x.Id == request.TicketId, cancellationToken);

        if (preTicket is null)
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.TicketNotFound);
        }

        if (preTicket.StudentId != request.ActorUserId || preTicket.Student.Role != UserRole.Student || !preTicket.Student.IsActive)
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var preDeviceError = OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            preTicket, request.DeviceId, request.KioskSessionToken, requireSessionToken: true, requireBoundDevice: true);
        if (preDeviceError is not null)
        {
            return Result<OfficialExamAudioSealResponse>.Failure(preDeviceError);
        }

        if (!string.Equals(preTicket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.TicketNotInProgress);
        }

        var preShift = await context.RealExamSessionShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == preTicket.ShiftId, cancellationToken);
        if (preShift is not null)
        {
            var preShiftWindowError = ExamShiftWindow.ValidateActiveSubmissionWindow(preShift, DateTime.UtcNow);
            if (preShiftWindowError is not null)
            {
                return Result<OfficialExamAudioSealResponse>.Failure(preShiftWindowError);
            }
        }

        if (string.IsNullOrWhiteSpace(preTicket.Student.StudentCode))
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.StudentNotFound);
        }

        var expectedObjectKey = OfficialExamAudioObjectKey.Create(
            preTicket.ShiftId,
            preTicket.Id,
            preTicket.SeatNumber,
            preTicket.Student.StudentCode);
        if (!string.Equals(expectedObjectKey, request.ObjectKey, StringComparison.Ordinal))
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.AudioObjectKeyMismatch);
        }

        if (string.IsNullOrWhiteSpace(request.Sha256))
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.AudioHashMismatch);
        }

        var normalizedSha256 = request.Sha256.Trim().ToLowerInvariant();
        if (preTicket.AudioVerifiedAt.HasValue)
        {
            if (string.Equals(preTicket.AudioObjectKey, expectedObjectKey, StringComparison.Ordinal) &&
                string.Equals(preTicket.AudioHashSha256, normalizedSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Result<OfficialExamAudioSealResponse>.Success(new(
                    preTicket.Id,
                    preTicket.AudioObjectKey!,
                    preTicket.AudioHashSha256!,
                    0,
                    preTicket.AudioVerifiedAt.Value));
            }

            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.AudioAlreadySealed);
        }

        var metadata = await storageService.GetObjectMetadataAsync(expectedObjectKey, cancellationToken);
        if (metadata is null)
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.AudioObjectNotFound);
        }

        if (metadata.ContentLength <= 0 ||
            metadata.ContentLength > OfficialExamRules.MaxOfficialExamAudioBytes ||
            !string.Equals(metadata.ContentType, "audio/webm", StringComparison.OrdinalIgnoreCase))
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.AudioObjectInvalid);
        }

        if (!await storageService.VerifySha256Async(expectedObjectKey, normalizedSha256, cancellationToken))
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.AudioHashMismatch);
        }

        // Pha 2 (TRONG ExecuteTicketDecisionAsync): đọc tươi vé (tracked), kiểm tra lại bất biến + ghi niêm phong nguyên tử.
        return await context.ExecuteTicketDecisionAsync(
            request.TicketId,
            async ct =>
            {
                var ticket = await context.StudentExamTickets
                    .Include(x => x.Student)
                    .FirstOrDefaultAsync(x => x.Id == request.TicketId, ct);

                if (ticket is null)
                {
                    return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.TicketNotFound);
                }

                if (ticket.StudentId != request.ActorUserId || ticket.Student.Role != UserRole.Student || !ticket.Student.IsActive)
                {
                    return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.Forbidden);
                }

                var deviceError = OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
                    ticket, request.DeviceId, request.KioskSessionToken, requireSessionToken: true, requireBoundDevice: true);
                if (deviceError is not null)
                {
                    return Result<OfficialExamAudioSealResponse>.Failure(deviceError);
                }

                if (!string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
                {
                    return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.TicketNotInProgress);
                }

                var shift = await context.RealExamSessionShifts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == ticket.ShiftId, ct);
                if (shift is not null)
                {
                    var shiftWindowError = ExamShiftWindow.ValidateActiveSubmissionWindow(shift, DateTime.UtcNow);
                    if (shiftWindowError is not null)
                    {
                        return Result<OfficialExamAudioSealResponse>.Failure(shiftWindowError);
                    }
                }

                if (ticket.AudioVerifiedAt.HasValue)
                {
                    if (string.Equals(ticket.AudioObjectKey, expectedObjectKey, StringComparison.Ordinal) &&
                        string.Equals(ticket.AudioHashSha256, normalizedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        return Result<OfficialExamAudioSealResponse>.Success(new(
                            ticket.Id,
                            ticket.AudioObjectKey!,
                            ticket.AudioHashSha256!,
                            0,
                            ticket.AudioVerifiedAt.Value));
                    }

                    return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.AudioAlreadySealed);
                }

                var verifiedAt = DateTime.UtcNow;
                ticket.AudioObjectKey = expectedObjectKey;
                ticket.AudioR2Url = expectedObjectKey;
                ticket.AudioHashSha256 = normalizedSha256;
                ticket.AudioVerifiedAt = verifiedAt;
                await context.SaveChangesAsync(ct);

                return Result<OfficialExamAudioSealResponse>.Success(new(
                    ticket.Id,
                    expectedObjectKey,
                    normalizedSha256,
                    metadata.ContentLength,
                    verifiedAt));
            },
            cancellationToken);
    }
}
