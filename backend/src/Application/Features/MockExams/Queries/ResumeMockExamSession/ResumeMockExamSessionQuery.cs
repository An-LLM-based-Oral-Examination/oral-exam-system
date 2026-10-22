using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;

namespace OralExamination.Application.Features.MockExams.Queries.ResumeMockExamSession;

/// <summary>
/// Query khôi phục ca thi thử đang in_progress để sinh viên tiếp tục làm bài.
/// </summary>
public sealed record ResumeMockExamSessionQuery(
    Guid SessionId,
    Guid StudentId
) : IRequest<Result<ResumeMockExamResponse>>;
