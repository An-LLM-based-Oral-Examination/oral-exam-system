using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateAudioUploadAuthorization;

public sealed class CreateAudioUploadAuthorizationCommandHandler(
    IApplicationDbContext context,
    IStorageService storageService)
    : IRequestHandler<CreateAudioUploadAuthorizationCommand, Result<OfficialExamAudioUploadAuthorizationResponse>>
{
    public async Task<Result<OfficialExamAudioUploadAuthorizationResponse>> Handle(
        CreateAudioUploadAuthorizationCommand request,
        CancellationToken cancellationToken)
    {
        var ticket = await context.StudentExamTickets
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Shift)
            .FirstOrDefaultAsync(x => x.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.TicketNotFound);
        }

        if (ticket.StudentId != request.ActorUserId || ticket.Student.Role != UserRole.Student)
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (!WorkstationAddress.Same(ticket.IpAddress, request.RemoteIpAddress))
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.WrongWorkstation);
        }

        if (!string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.TicketNotInProgress);
        }

        if (!ExamShiftWindow.IsOpenStatus(ticket.Shift.Status) || !ExamShiftWindow.Contains(ticket.Shift, request.AtUtc))
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.ShiftNotOpen);
        }

        if (ticket.AudioVerifiedAt.HasValue)
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.AudioAlreadySealed);
        }

        if (string.IsNullOrWhiteSpace(ticket.Student.StudentCode))
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.StudentNotFound);
        }

        var objectKey = OfficialExamAudioObjectKey.Create(
            ticket.ShiftId,
            ticket.Id,
            ticket.SeatNumber,
            ticket.Student.StudentCode);
        var expiresIn = TimeSpan.FromMinutes(OfficialExamRules.AudioUploadUrlLifetimeMinutes);
        var upload = await storageService.CreatePresignedUploadUrlAsync(
            objectKey,
            "audio/webm",
            expiresIn,
            cancellationToken);

        return Result<OfficialExamAudioUploadAuthorizationResponse>.Success(new(
            upload.ObjectKey,
            Path.GetFileName(upload.ObjectKey),
            upload.UploadUrl,
            upload.ExpiresAt,
            upload.RequiredHeaders));
    }
}
