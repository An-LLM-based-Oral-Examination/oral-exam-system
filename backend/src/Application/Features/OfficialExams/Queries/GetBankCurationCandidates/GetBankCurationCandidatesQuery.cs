using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetBankCurationCandidates;

/// <summary>
/// Truy vấn danh sách câu hỏi tạo tay (PENDING_CURATION) để Trưởng Bộ Môn tổng hợp lọc trùng 70-79% (vàng) / >=80% (đỏ)
/// và quyết định nạp vào kho chọn chung kỳ sau hoặc loại khỏi kho.
/// </summary>
public sealed record GetBankCurationCandidatesQuery(
    Guid CourseId,
    Guid ActorUserId,
    string? ActorRole = null,
    string? Band = null) : IRequest<Result<BankCurationCandidatesResponseDto>>;
