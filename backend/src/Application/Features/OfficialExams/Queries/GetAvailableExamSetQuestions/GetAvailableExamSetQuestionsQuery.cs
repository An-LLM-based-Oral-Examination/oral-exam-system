using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetAvailableExamSetQuestions;

/// <summary>
/// Truy vấn danh sách câu hỏi khả dụng để soạn bộ đề thi thật theo cấu trúc đề (MF-03 M3).
/// Ẩn hoàn toàn câu thuộc Đề mẫu, câu đã duyệt trong đợt thi hiện tại, và (với GV) câu chính GV đã chọn ở bộ đề trước.
/// Gắn nhãn vàng ẩn danh cho câu đang chờ duyệt ở bộ đề của GV khác.
/// </summary>
public sealed record GetAvailableExamSetQuestionsQuery(
    Guid StructureId,
    Guid ActorUserId,
    string? ActorRole = null,
    Guid? CloId = null,
    string? Difficulty = null,
    string? BloomLevel = null,
    string? Search = null, Guid? SessionId = null, Guid? ExcludeExamSetId = null) : IRequest<Result<AvailableExamSetQuestionsResponseDto>>;
