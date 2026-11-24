using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.ReturnExamSetForRevision;

/// <summary>
/// Trưởng Bộ Môn trả bộ đề đang chờ duyệt (SUBMITTED_FOR_REVIEW) về trạng thái REJECTED kèm lý do để GV thay câu/chỉnh sửa.
/// </summary>
public sealed record ReturnExamSetForRevisionCommand(
    Guid ExamSetId,
    string ReviewNotes,
    Guid ActorUserId,
    string? ActorRole = null) : IRequest<Result<ReturnExamSetForRevisionResultDto>>;
