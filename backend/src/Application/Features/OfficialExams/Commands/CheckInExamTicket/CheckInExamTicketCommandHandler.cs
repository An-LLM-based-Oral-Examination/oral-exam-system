using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;

public sealed class CheckInExamTicketCommandHandler : IRequestHandler<CheckInExamTicketCommand, Result<ExamTicketResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<CheckInExamTicketCommand> _validator;

    public CheckInExamTicketCommandHandler(
        IApplicationDbContext context,
        IValidator<CheckInExamTicketCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<ExamTicketResponse>> Handle(CheckInExamTicketCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ExamTicketResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var actor = await _context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive ||
            actor.Role is not (UserRole.Student or UserRole.Proctor or UserRole.Lecturer or UserRole.DepartmentHead))
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        string? normalizedDeviceId = null;
        if (!string.IsNullOrWhiteSpace(request.DeviceId))
        {
            if (!OfficialExamDeviceGuard.TryNormalizeDeviceId(request.DeviceId, out var parsedDeviceId))
            {
                return Result<ExamTicketResponse>.Failure(OfficialExamErrors.DeviceIdInvalid);
            }
            normalizedDeviceId = parsedDeviceId;
        }

        try
        {
            return await _context.ExecuteTicketDecisionAsync(
                request.TicketId,
                async ct =>
                {
                    var ticket = await _context.StudentExamTickets
                        .Include(item => item.Shift)
                        .FirstOrDefaultAsync(item => item.Id == request.TicketId, ct);
                    if (ticket is null)
                    {
                        return Result<ExamTicketResponse>.Failure(OfficialExamErrors.TicketNotFound);
                    }

                    var isOwner = actor.Id == ticket.StudentId && actor.Role == UserRole.Student;
                    var isProctor = ticket.Shift.ProctorUserId == actor.Id
                        && (actor.Role == UserRole.Proctor || actor.Role == UserRole.Lecturer);
                    if (!isOwner && !isProctor)
                    {
                        return Result<ExamTicketResponse>.Failure(OfficialExamErrors.Forbidden);
                    }

                    if (ticket.SeatNumber < OfficialExamRules.MinSeat || ticket.SeatNumber > OfficialExamRules.MaxSeat)
                    {
                        return Result<ExamTicketResponse>.Failure(OfficialExamErrors.SeatOutOfRange);
                    }

                    if (!ExamShiftWindow.IsOpenStatus(ticket.Shift.Status))
                    {
                        return Result<ExamTicketResponse>.Failure(OfficialExamErrors.ShiftClosed);
                    }

                    if (!ExamShiftWindow.Contains(ticket.Shift, request.AtUtc))
                    {
                        return Result<ExamTicketResponse>.Failure(OfficialExamErrors.ShiftNotOpen);
                    }

                    // BYOD Desktop/Web Kiosk: thí sinh bắt buộc gửi X-Device-Id hợp lệ khi điểm danh vào thi hoặc khôi phục phiên.
                    if (isOwner && normalizedDeviceId is null)
                    {
                        return Result<ExamTicketResponse>.Failure(OfficialExamErrors.DeviceIdInvalid);
                    }

                    // ip_address is audit-only in BYOD mode: only mutate tracked entity right before SaveChangesAsync.
                    var hasNormalizedRemoteIp = WorkstationAddress.TryNormalize(request.RemoteIpAddress, out var normalizedRemoteIp);

                    var nowUtc = DateTime.UtcNow;
                    string? rawSessionToken = null;

                    if (string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.OrdinalIgnoreCase))
                    {
                        // Same-device crash/F5 recovery on an already checked-in BYOD ticket rotates the active session token.
                        if (!isOwner)
                        {
                            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.TicketNotScheduled);
                        }

                        if (!string.IsNullOrWhiteSpace(ticket.DeviceId))
                        {
                            if (normalizedDeviceId is null
                                || !string.Equals(ticket.DeviceId, normalizedDeviceId, StringComparison.Ordinal))
                            {
                                return Result<ExamTicketResponse>.Failure(OfficialExamErrors.DeviceMismatch);
                            }

                            if (hasNormalizedRemoteIp)
                            {
                                ticket.IpAddress = normalizedRemoteIp;
                            }

                            rawSessionToken = OfficialExamDeviceGuard.GenerateSessionToken();
                            ticket.DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(rawSessionToken);

                            _context.AuditLogs.Add(new AuditLog
                            {
                                Id = Guid.NewGuid(),
                                UserId = actor.Id,
                                Action = "KIOSK_SESSION_RESUMED",
                                EntityName = "student_exam_tickets",
                                EntityId = ticket.Id,
                                NewValues = JsonSerializer.Serialize(new
                                {
                                    ticketId = ticket.Id,
                                    deviceId = ticket.DeviceId,
                                    kioskVersion = request.KioskVersion
                                }),
                                CreatedAt = nowUtc
                            });

                            await _context.SaveChangesAsync(ct);

                            var resumed = AssignExamTicketCommandHandler.ToResponse(ticket);
                            return Result<ExamTicketResponse>.Success(resumed with { ActiveSessionToken = rawSessionToken });
                        }

                        if (normalizedDeviceId is not null)
                        {
                            // Ticket was unlocked by EO while IN_PROGRESS -> bind to replacement BYOD laptop now!
                            var deviceTaken = await _context.StudentExamTickets
                                .AsNoTracking()
                                .AnyAsync(
                                    t => t.ShiftId == ticket.ShiftId
                                        && t.Id != ticket.Id
                                        && t.DeviceId == normalizedDeviceId,
                                    ct);
                            if (deviceTaken)
                            {
                                return Result<ExamTicketResponse>.Failure(OfficialExamErrors.DeviceInUseOtherTicket);
                            }

                            if (hasNormalizedRemoteIp)
                            {
                                ticket.IpAddress = normalizedRemoteIp;
                            }

                            rawSessionToken = OfficialExamDeviceGuard.GenerateSessionToken();
                            ticket.DeviceId = normalizedDeviceId;
                            ticket.DeviceLockedAt = nowUtc;
                            ticket.DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(rawSessionToken);

                            _context.AuditLogs.Add(new AuditLog
                            {
                                Id = Guid.NewGuid(),
                                UserId = actor.Id,
                                Action = "KIOSK_DEVICE_REBOUND",
                                EntityName = "student_exam_tickets",
                                EntityId = ticket.Id,
                                NewValues = JsonSerializer.Serialize(new
                                {
                                    ticketId = ticket.Id,
                                    deviceId = ticket.DeviceId,
                                    kioskVersion = request.KioskVersion
                                }),
                                CreatedAt = nowUtc
                            });

                            await _context.SaveChangesAsync(ct);

                            var rebound = AssignExamTicketCommandHandler.ToResponse(ticket);
                            return Result<ExamTicketResponse>.Success(rebound with { ActiveSessionToken = rawSessionToken });
                        }

                        return Result<ExamTicketResponse>.Failure(OfficialExamErrors.TicketNotScheduled);
                    }

                    if (!string.Equals(ticket.Status, ExamTicketStatus.Scheduled, StringComparison.OrdinalIgnoreCase))
                    {
                        return Result<ExamTicketResponse>.Failure(OfficialExamErrors.TicketNotScheduled);
                    }

                    if (!string.IsNullOrWhiteSpace(ticket.DeviceId)
                        && (normalizedDeviceId is null
                            || !string.Equals(ticket.DeviceId, normalizedDeviceId, StringComparison.Ordinal)))
                    {
                        return Result<ExamTicketResponse>.Failure(OfficialExamErrors.DeviceMismatch);
                    }

                    if (normalizedDeviceId is not null)
                    {
                        var deviceInUse = await _context.StudentExamTickets
                            .AsNoTracking()
                            .AnyAsync(
                                t => t.ShiftId == ticket.ShiftId
                                    && t.Id != ticket.Id
                                    && t.DeviceId == normalizedDeviceId,
                                ct);
                        if (deviceInUse)
                        {
                            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.DeviceInUseOtherTicket);
                        }

                        rawSessionToken = OfficialExamDeviceGuard.GenerateSessionToken();
                        ticket.DeviceId = normalizedDeviceId;
                        ticket.DeviceLockedAt = nowUtc;
                        ticket.DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(rawSessionToken);
                    }

                    if (!ticket.ExamSetId.HasValue)
                    {
                        var resolvedSetId = await AssignExamTicketCommandHandler.TryResolveHandedOverExamSetIdAsync(
                            _context,
                            ticket.Shift,
                            ct);
                        if (!resolvedSetId.HasValue)
                        {
                            return Result<ExamTicketResponse>.Failure(
                                $"CONFLICT: {OfficialExamErrors.NoAvailableExamSet}");
                        }

                        ticket.ExamSetId = resolvedSetId;
                    }

                    if (hasNormalizedRemoteIp)
                    {
                        ticket.IpAddress = normalizedRemoteIp;
                    }

                    ticket.Status = ExamTicketStatus.InProgress;
                    ticket.CheckedInAt = nowUtc;
                    await _context.SaveChangesAsync(ct);

                    var response = AssignExamTicketCommandHandler.ToResponse(ticket);
                    return Result<ExamTicketResponse>.Success(response with { ActiveSessionToken = rawSessionToken });
                },
                cancellationToken);
        }
        catch (DbUpdateException ex) when (IsShiftDeviceUniqueViolation(ex))
        {
            return Result<ExamTicketResponse>.Failure(OfficialExamErrors.DeviceInUseOtherTicket);
        }
    }

    private static bool IsShiftDeviceUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null)
        {
            return false;
        }

        var innerType = inner.GetType();
        var sqlState = innerType.GetProperty("SqlState")?.GetValue(inner) as string;
        if (!string.Equals(sqlState, "23505", StringComparison.Ordinal))
        {
            return false;
        }

        var constraintName = innerType.GetProperty("ConstraintName")?.GetValue(inner) as string;
        return string.IsNullOrWhiteSpace(constraintName)
            || string.Equals(constraintName, "uq_tickets_shift_device", StringComparison.OrdinalIgnoreCase);
    }
}
