using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;

namespace OralExamination.Application.Features.InClassShifts.Commands.AdjustInClassSubmissionScore;

public sealed record AdjustInClassSubmissionScoreCommand(
    Guid ActorUserId,
    Guid SubmissionId,
    decimal FinalScore,
    string? LecturerNotes
) : IRequest<Result<InClassExamSubmissionResponse>>;
