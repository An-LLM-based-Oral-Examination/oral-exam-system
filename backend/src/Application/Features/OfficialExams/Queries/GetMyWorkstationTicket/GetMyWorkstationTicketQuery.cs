using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;

public sealed record GetMyWorkstationTicketQuery(
    Guid ActorUserId,
    string? RemoteIpAddress,
    DateTime AtUtc,
    string? DeviceId = null,
    string? KioskSessionToken = null) : IRequest<Result<StudentWorkstationTicketResponse>>;
