using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.UnlockExamTicketDevice;

public sealed class UnlockExamTicketDeviceCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UnlockExamTicketDeviceCommand, Result<UnlockExamTicketDeviceResponse>>
{
    public async Task<Result<UnlockExamTicketDeviceResponse>> Handle(
        UnlockExamTicketDeviceCommand request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveOfficerAsync(context, request.ActorUserId, cancellationToken))
        {
            return Result<UnlockExamTicketDeviceResponse>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        if (request.TicketId == Guid.Empty)
        {
            return Result<UnlockExamTicketDeviceResponse>.Failure($"NOT_FOUND: {OfficialExamErrors.TicketNotFound}");
        }

        var trimmedReason = request.Reason?.Trim() ?? string.Empty;
        if (trimmedReason.Length < OfficialExamRules.MinUnlockDeviceReasonLength
            || trimmedReason.Length > OfficialExamRules.MaxUnlockDeviceReasonLength)
        {
            return Result<UnlockExamTicketDeviceResponse>.Failure(
                $"UNPROCESSABLE: Lý do mở khóa thiết bị bắt buộc từ {OfficialExamRules.MinUnlockDeviceReasonLength} đến {OfficialExamRules.MaxUnlockDeviceReasonLength} ký tự.");
        }

        return await context.ExecuteTicketDecisionAsync(
            request.TicketId,
            async ct =>
            {
                var ticket = await context.StudentExamTickets
                    .Include(t => t.Student)
                    .FirstOrDefaultAsync(t => t.Id == request.TicketId, ct);

                if (ticket is null)
                {
                    return Result<UnlockExamTicketDeviceResponse>.Failure($"NOT_FOUND: {OfficialExamErrors.TicketNotFound}");
                }

                if (ticket.IsLocked
                    || ticket.GradesPublishedAt.HasValue
                    || ticket.SubmittedAt.HasValue
                    || ticket.Status is ExamTicketStatus.Submitted
                        or ExamTicketStatus.AiGraded
                        or ExamTicketStatus.Audited
                        or ExamTicketStatus.Published)
                {
                    return Result<UnlockExamTicketDeviceResponse>.Failure(
                        "CONFLICT: Không thể mở khóa thiết bị cho vé thi đã nộp bài, đã khóa niêm phong hoặc đã công bố điểm.");
                }

                if (string.IsNullOrWhiteSpace(ticket.DeviceId))
                {
                    return Result<UnlockExamTicketDeviceResponse>.Failure(
                        "CONFLICT: Vé thi hiện chưa được gắn với thiết bị BYOD nào.");
                }

                var previousDeviceId = ticket.DeviceId;
                var previousStatus = ticket.Status;
                var nowUtc = DateTime.UtcNow;

                ticket.DeviceId = null;
                ticket.DeviceLockedAt = null;
                ticket.DeviceSessionTokenHash = null;
                ticket.DeviceUnlockCount += 1;
                ticket.LastDeviceUnlockedBy = request.ActorUserId;
                ticket.LastDeviceUnlockedAt = nowUtc;
                if (string.Equals(ticket.Status, ExamTicketStatus.Locked, StringComparison.OrdinalIgnoreCase))
                {
                    ticket.Status = ExamTicketStatus.InProgress;
                }

                var studentCode = ticket.Student?.StudentCode ?? string.Empty;

                context.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = request.ActorUserId,
                    Action = "OFFICIAL_TICKET_DEVICE_UNLOCKED",
                    EntityName = "student_exam_tickets",
                    EntityId = ticket.Id,
                    OldValues = JsonSerializer.Serialize(new { deviceId = previousDeviceId, status = previousStatus }),
                    NewValues = JsonSerializer.Serialize(new
                    {
                        ticketId = ticket.Id,
                        shiftId = ticket.ShiftId,
                        studentCode,
                        seatNumber = ticket.SeatNumber,
                        previousDeviceId,
                        previousStatus,
                        newStatus = ticket.Status,
                        deviceUnlockCount = ticket.DeviceUnlockCount,
                        reason = trimmedReason
                    }),
                    CreatedAt = nowUtc
                });

                await context.SaveChangesAsync(ct);

                return Result<UnlockExamTicketDeviceResponse>.Success(new UnlockExamTicketDeviceResponse(
                    ticket.Id,
                    ticket.ShiftId,
                    studentCode,
                    ticket.SeatNumber,
                    previousDeviceId,
                    ticket.DeviceUnlockCount,
                    request.ActorUserId,
                    nowUtc,
                    trimmedReason));
            },
            cancellationToken);
    }
}
