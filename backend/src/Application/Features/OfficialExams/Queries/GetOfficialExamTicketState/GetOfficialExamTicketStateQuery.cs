using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamTicketState;

/// <summary>
/// Truy xuất trạng thái khôi phục Kiosk cho sinh viên sở hữu vé thi (F5/reconnect recovery, 0% lộ điểm).
/// </summary>
public sealed record GetOfficialExamTicketStateQuery(
    Guid ActorUserId,
    Guid TicketId,
    string? DeviceId = null,
    string? KioskSessionToken = null)
    : IRequest<Result<OfficialExamTicketStateResponse>>;
