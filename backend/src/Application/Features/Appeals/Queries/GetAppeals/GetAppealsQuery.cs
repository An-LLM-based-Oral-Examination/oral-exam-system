using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;

namespace OralExamination.Application.Features.Appeals.Queries.GetAppeals;

/// <summary>
/// CQRS Query tra cứu danh sách đơn phúc khảo có hỗ trợ bộ lọc.
/// </summary>
public sealed record GetAppealsQuery(
    string? Status = null,
    Guid? SessionId = null,
    Guid? StudentId = null,
    Guid? AssignedTo = null
) : IRequest<Result<List<AppealResponseDto>>>;
