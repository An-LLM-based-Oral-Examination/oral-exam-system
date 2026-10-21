using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Student.DTOs;

namespace OralExamination.Application.Features.Student.Queries.GetOfficialExamGrade;

/// <summary>
/// Truy vấn chi tiết điểm thi thật và Scorecard theo tiêu chí Rubric của sinh viên sau khi điểm đã được công bố (MF-04).
/// </summary>
public sealed record GetOfficialExamGradeQuery(
    Guid TicketId,
    Guid StudentId) : IRequest<Result<StudentOfficialExamGradeDto>>;
