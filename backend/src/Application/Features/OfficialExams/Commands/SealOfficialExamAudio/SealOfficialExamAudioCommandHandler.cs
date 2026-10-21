using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
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
        var ticket = await context.StudentExamTickets
            .Include(x => x.Student)
            .FirstOrDefaultAsync(x => x.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.TicketNotFound);
        }

        if (ticket.StudentId != request.ActorUserId || ticket.Student.Role != UserRole.Student)
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (!WorkstationAddress.Same(ticket.IpAddress, request.RemoteIpAddress))
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.WrongWorkstation);
        }

        if (!string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.TicketNotInProgress);
        }

        if (string.IsNullOrWhiteSpace(ticket.Student.StudentCode))
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.StudentNotFound);
        }

        var expectedObjectKey = OfficialExamAudioObjectKey.Create(
            ticket.ShiftId,
            ticket.Id,
            ticket.SeatNumber,
            ticket.Student.StudentCode);
        if (!string.Equals(expectedObjectKey, request.ObjectKey, StringComparison.Ordinal))
        {
            return Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.AudioObjectKeyMismatch);
        }

        var normalizedSha256 = request.Sha256!.ToLowerInvariant();
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

        var verifiedAt = DateTime.UtcNow;
        ticket.AudioObjectKey = expectedObjectKey;
        ticket.AudioR2Url = expectedObjectKey;
        ticket.AudioHashSha256 = normalizedSha256;
        ticket.AudioVerifiedAt = verifiedAt;
        await context.SaveChangesAsync(cancellationToken);

        return Result<OfficialExamAudioSealResponse>.Success(new(
            ticket.Id,
            expectedObjectKey,
            normalizedSha256,
            metadata.ContentLength,
            verifiedAt));
    }
}
