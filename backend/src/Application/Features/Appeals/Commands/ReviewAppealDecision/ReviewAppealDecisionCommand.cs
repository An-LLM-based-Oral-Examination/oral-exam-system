using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;

namespace OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;

/// <summary>
/// CQRS Command thẩm định và ra quyết định xử lý đơn phúc khảo (APPROVED / REJECTED).
/// Dành cho Trưởng Bộ Môn (department_head) hoặc Admin.
/// </summary>
public sealed record ReviewAppealDecisionCommand(
    Guid AppealId,
    Guid ReviewerId,
    string Decision,
    decimal? ProposedScore,
    string ReviewNotes
) : IRequest<Result<AppealResponseDto>>;
