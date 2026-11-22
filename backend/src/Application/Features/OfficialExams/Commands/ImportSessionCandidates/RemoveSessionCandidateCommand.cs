using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.OfficialExams.Commands.ImportSessionCandidates;

public sealed record RemoveSessionCandidateCommand(
    Guid ActorUserId,
    Guid SessionId,
    Guid StudentId) : IRequest<Result<bool>>;
