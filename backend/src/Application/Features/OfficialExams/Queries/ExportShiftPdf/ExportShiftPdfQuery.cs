using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.ExportShiftPdf;

/// <summary>
/// Query trích xuất biên bản bảng điểm ca thi ra tệp PDF có chữ ký số SHA-256 và liên kết QR (BE-V01).
/// </summary>
public sealed record ExportShiftPdfQuery(Guid ShiftId) : IRequest<Result<ExportShiftFileResponseDto>>;
