using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.Appeals.Queries.GetAppealEvidence;

/// <summary>
/// Truy xuất bằng chứng đơn lẻ của đúng 1 vé thi đang phúc khảo (BOLA-locked, không rò rỉ sinh viên khác trong ca).
/// </summary>
public sealed record GetAppealEvidenceQuery(Guid AppealId, Guid ActorUserId) : IRequest<Result<AuditEvidenceItemDto>>;
