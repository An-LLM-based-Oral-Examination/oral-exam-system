using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;

namespace OralExamination.Application.Features.Appeals.Commands.RegradeAppeal;

public sealed record RegradeAppealCommand(
    Guid AppealId,
    Guid LecturerId,
    decimal NewScore,
    string ReviewNotes) : IRequest<Result<AppealResponseDto>>;
