using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.ConfigureOfficialExamFollowUp;

public sealed record ConfigureOfficialExamFollowUpCommand(
    Guid ActorUserId,
    Guid SessionId,
    bool HasFollowUp,
    int MaxFollowUpQuestions = 2) : IRequest<Result<OfficialExamSessionResponse>>;
