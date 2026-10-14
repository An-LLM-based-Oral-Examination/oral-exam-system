using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;

namespace OralExamination.Application.Features.MockExams.Queries.GetMockExamHistory;

/// <summary>
/// Query tra cứu lịch sử thi thử của sinh viên có phân trang và lọc theo môn học (MF-02 - FE-02).
/// </summary>
public sealed record GetMockExamHistoryQuery(
    Guid StudentId,
    Guid? CourseId = null,
    int PageIndex = 1,
    int PageSize = 10
) : IRequest<Result<MockExamHistoryListResponse>>;
