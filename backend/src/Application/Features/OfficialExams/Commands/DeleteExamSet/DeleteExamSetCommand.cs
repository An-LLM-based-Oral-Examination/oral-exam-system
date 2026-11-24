using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.OfficialExams.Commands.DeleteExamSet;

public sealed record DeleteExamSetCommand(
    Guid ExamSetId,
    Guid ActorUserId,
    string? ActorRole = null) : IRequest<Result>;
