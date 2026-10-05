using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;

/// <summary>
/// CQRS Query trích xuất bằng chứng hậu kiểm (Evidence Panel) cho ca thi phòng Lab (MF-04).
/// </summary>
public sealed record GetAuditEvidenceQuery(Guid ShiftId) : IRequest<Result<List<AuditEvidenceItemDto>>>;
