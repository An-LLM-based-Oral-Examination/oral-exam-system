using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;

namespace OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;

/// <summary>
/// Query tra cứu hạn ngạch thi thử trong ngày của sinh viên theo môn học (MF-02).
/// </summary>
public sealed record GetMockExamQuotaQuery(
    Guid CourseId,
    Guid StudentId
) : IRequest<Result<MockExamQuotaDto>>;
