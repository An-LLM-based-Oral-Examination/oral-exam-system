using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;

public sealed record ApproveExamSetCommand(
    Guid ExamSetId,
    string? ReviewNotes,
    Guid ActorUserId,
    string? ActorRole = null) : IRequest<Result<ApproveExamSetResultDto>>;
