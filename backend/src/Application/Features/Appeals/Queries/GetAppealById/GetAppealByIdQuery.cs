using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;

namespace OralExamination.Application.Features.Appeals.Queries.GetAppealById;

/// <summary>
/// CQRS Query tra cứu thông tin chi tiết một đơn phúc khảo theo ID.
/// </summary>
public sealed record GetAppealByIdQuery(Guid Id) : IRequest<Result<AppealResponseDto>>;
