using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;

namespace OralExamination.Application.Features.Appeals.Commands.BatchAssignAppeals;

public sealed record BatchAssignAppealsCommand(
    Guid OfficerId,
    List<AppealAssignmentItemDto> Assignments) : IRequest<Result<BatchAssignAppealsResponseDto>>;
