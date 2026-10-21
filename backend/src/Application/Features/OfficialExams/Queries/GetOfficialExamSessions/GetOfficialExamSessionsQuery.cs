using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSessions;

/// <summary>
/// Truy vấn danh sách kỳ thi thật (OfficialExamSession) có hỗ trợ lọc theo môn học và trạng thái.
/// </summary>
public sealed record GetOfficialExamSessionsQuery(
    Guid? CourseId = null,
    string? Status = null) : IRequest<Result<List<OfficialExamSessionResponse>>>;
