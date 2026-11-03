using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.OfficialExams.Commands.HandOverExamSet;

public sealed record HandOverExamSetCommand(
    Guid ExamSetId,
    Guid ActorUserId,
    string? ActorRole = null) : IRequest<Result>;
