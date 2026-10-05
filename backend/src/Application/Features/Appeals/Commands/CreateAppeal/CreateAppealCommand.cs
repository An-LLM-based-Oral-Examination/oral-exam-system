using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;

namespace OralExamination.Application.Features.Appeals.Commands.CreateAppeal;

/// <summary>
/// CQRS Command nộp đơn phúc khảo nội bộ của sinh viên (MF-04).
/// </summary>
public sealed record CreateAppealCommand(
    Guid StudentId,
    Guid TicketId,
    Guid? SubmissionId,
    string Reason
) : IRequest<Result<AppealResponseDto>>;
