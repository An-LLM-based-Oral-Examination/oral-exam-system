using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;

namespace OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;

/// <summary>
/// Query tra cứu chi tiết một bài thi thử trong quá khứ kèm Bảng điểm Scorecard (MF-02 - FE-02).
/// </summary>
public sealed record GetMockExamSessionDetailQuery(
    Guid SessionId,
    Guid StudentId,
    bool IsElevatedRole = false
) : IRequest<Result<MockExamScorecardDto>>;
