using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.Practice.Commands.CompletePracticeSession;

/// <summary>
/// Command kết thúc phiên luyện tập, cập nhật trạng thái completed và thời gian kết thúc.
/// </summary>
public sealed record CompletePracticeSessionCommand(
    Guid SessionId,
    Guid StudentId
) : IRequest<Result>;
