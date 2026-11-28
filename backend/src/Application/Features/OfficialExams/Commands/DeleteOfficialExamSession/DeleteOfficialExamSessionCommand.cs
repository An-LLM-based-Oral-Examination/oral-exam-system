using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.OfficialExams.Commands.DeleteOfficialExamSession;

public sealed record DeleteOfficialExamSessionCommand(
    Guid ActorUserId,
    string? ActorRole,
    Guid SessionId) : IRequest<Result>;
