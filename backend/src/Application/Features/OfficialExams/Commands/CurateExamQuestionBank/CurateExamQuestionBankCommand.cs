using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.CurateExamQuestionBank;

/// <summary>
/// Trưởng Bộ Môn quyết định nạp kho (CURATED_INTO_BANK) hoặc loại khỏi kho chọn chung (EXCLUDED_FROM_BANK)
/// cho một hoặc nhiều câu hỏi tạo tay đang chờ lọc (PENDING_CURATION).
/// </summary>
public sealed record CurateExamQuestionBankCommand(
    Guid CourseId,
    List<Guid> QuestionIds,
    string Decision,
    string? Notes,
    Guid ActorUserId,
    string? ActorRole = null) : IRequest<Result<CurateExamQuestionBankResultDto>>;
