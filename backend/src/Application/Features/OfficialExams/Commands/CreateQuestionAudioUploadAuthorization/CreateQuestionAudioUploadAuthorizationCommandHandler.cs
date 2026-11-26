using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateQuestionAudioUploadAuthorization;

public sealed class CreateQuestionAudioUploadAuthorizationCommandHandler(
    IApplicationDbContext context,
    IStorageService storageService)
    : IRequestHandler<CreateQuestionAudioUploadAuthorizationCommand, Result<OfficialExamAudioUploadAuthorizationResponse>>
{
    public async Task<Result<OfficialExamAudioUploadAuthorizationResponse>> Handle(
        CreateQuestionAudioUploadAuthorizationCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FollowUpIndex < 0 || request.FollowUpIndex > OfficialExamRules.MaxFollowUpQuestions)
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(
                $"UNPROCESSABLE: Chỉ số lượt câu hỏi phụ (followUpIndex) phải từ 0 đến {OfficialExamRules.MaxFollowUpQuestions}.");
        }

        var ticket = await context.StudentExamTickets
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Shift)
            .FirstOrDefaultAsync(x => x.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.TicketNotFound);
        }

        if (ticket.StudentId != request.ActorUserId || ticket.Student.Role != UserRole.Student || !ticket.Student.IsActive)
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var deviceError = OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            ticket, request.DeviceId, request.KioskSessionToken, requireSessionToken: true, requireBoundDevice: true);
        if (deviceError is not null)
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(deviceError);
        }

        if (!string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal))
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.TicketNotInProgress);
        }

        if (!ExamShiftWindow.IsOpenStatus(ticket.Shift.Status) || !ExamShiftWindow.Contains(ticket.Shift, request.AtUtc))
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.ShiftNotOpen);
        }

        if (!ticket.ExamSetId.HasValue)
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.ExamSetNotAssigned);
        }

        if (string.IsNullOrWhiteSpace(ticket.Student.StudentCode))
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.StudentNotFound);
        }

        var examSetQuestion = await context.ExamSetQuestions
            .AsNoTracking()
            .Where(esq => esq.ExamSetId == ticket.ExamSetId.Value && esq.ExamQuestionId == request.QuestionId)
            .Select(esq => new { esq.OrderIndex })
            .FirstOrDefaultAsync(cancellationToken);

        if (examSetQuestion is null)
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.SubmissionQuestionsMismatch);
        }

        if (request.FollowUpIndex > 0)
        {
            var session = await context.OfficialExamSessions
                .AsNoTracking()
                .Where(s => s.Id == ticket.Shift.SessionId)
                .Select(s => new { s.HasFollowUp, s.MaxFollowUpQuestions })
                .FirstOrDefaultAsync(cancellationToken);

            if (session is null || !session.HasFollowUp || request.FollowUpIndex > session.MaxFollowUpQuestions)
            {
                return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.FollowUpNotEnabled);
            }
        }

        var alreadyRecorded = await context.ExamQuestionSubmissions
            .AsNoTracking()
            .AnyAsync(
                s => s.TicketId == ticket.Id
                    && s.ExamQuestionId == request.QuestionId
                    && s.FollowUpIndex == request.FollowUpIndex
                    && s.AudioVerifiedAt != null,
                cancellationToken);

        if (alreadyRecorded)
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure(OfficialExamErrors.AudioAlreadySealed);
        }

        var objectKey = OfficialExamAudioObjectKey.CreateForQuestion(
            ticket.ShiftId,
            ticket.Id,
            ticket.SeatNumber,
            ticket.Student.StudentCode,
            examSetQuestion.OrderIndex,
            request.FollowUpIndex);

        var expiresIn = TimeSpan.FromMinutes(OfficialExamRules.AudioUploadUrlLifetimeMinutes);

        StoragePresignedUploadResult upload;
        try
        {
            upload = await storageService.CreatePresignedUploadUrlAsync(
                objectKey,
                "audio/webm",
                expiresIn,
                request.ExpectedSha256Base64,
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Result<OfficialExamAudioUploadAuthorizationResponse>.Failure($"UNPROCESSABLE: {ex.Message}");
        }

        return Result<OfficialExamAudioUploadAuthorizationResponse>.Success(new OfficialExamAudioUploadAuthorizationResponse(
            upload.ObjectKey,
            Path.GetFileName(upload.ObjectKey),
            upload.UploadUrl,
            upload.ExpiresAt,
            upload.RequiredHeaders));
    }
}
