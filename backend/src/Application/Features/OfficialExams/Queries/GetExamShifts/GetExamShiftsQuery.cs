using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetExamShifts;

/// <summary>
/// Truy vấn danh sách ca thi phòng Lab (RealExamSessionShift) có hỗ trợ lọc theo kỳ thi, giám thị và trạng thái.
/// </summary>
public sealed record GetExamShiftsQuery(
    Guid? SessionId = null,
    Guid? ProctorUserId = null,
    string? Status = null) : IRequest<Result<List<ExamShiftResponse>>>;
