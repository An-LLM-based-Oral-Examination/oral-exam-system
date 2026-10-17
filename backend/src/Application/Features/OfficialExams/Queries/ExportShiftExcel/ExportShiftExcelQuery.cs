using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.ExportShiftExcel;

/// <summary>
/// Query trích xuất biên bản bảng điểm ca thi ra tệp Excel (.xlsx) chuẩn phòng Khảo thí ĐH FPT (BE-V01).
/// </summary>
public sealed record ExportShiftExcelQuery(Guid ShiftId) : IRequest<Result<ExportShiftFileResponseDto>>;
