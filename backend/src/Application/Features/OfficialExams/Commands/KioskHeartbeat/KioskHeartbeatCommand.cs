using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.OfficialExams.Commands.KioskHeartbeat;

public sealed record KioskHeartbeatCommand(
    Guid ActorUserId,
    Guid TicketId,
    string? DeviceId,
    string? KioskSessionToken,
    string? KioskVersion,
    string? RemoteIpAddress,
    int? AudioLevelDb = null,
    bool? IsFullscreen = null,
    int? DisplayCount = null) : IRequest<Result<KioskHeartbeatResponse>>;

public sealed record KioskHeartbeatResponse(
    Guid TicketId,
    Guid ShiftId,
    string TicketStatus,
    string ShiftStatus,
    DateTime ServerUtcNow,
    int RemainingShiftSeconds,
    bool IsLocked,
    bool DeviceUnlockRequired,
    bool ShouldTerminateExam,
    string? Notice);
