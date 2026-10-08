using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.DTOs;

namespace OralExamination.Application.Features.Practice.Queries.GetPracticeSession;

/// <summary>
/// Query lấy chi tiết phiên luyện tập bao gồm danh sách câu hỏi và câu trả lời đã nộp.
/// </summary>
public sealed record GetPracticeSessionQuery(Guid SessionId) : IRequest<Result<PracticeSessionDetailDto>>;
