using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;

namespace OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;

/// <summary>
/// Command nộp bài thi thử chính thức và kích hoạt chấm điểm tức thì (MF-02).
/// Hỗ trợ kiểm tra Server Master Timer, Network Grace Period 10s, và trả về Bảng điểm Instant Scorecard.
/// </summary>
public sealed record SubmitMockExamSessionCommand(
    Guid SessionId,
    Guid StudentId
) : IRequest<Result<MockExamScorecardDto>>;
