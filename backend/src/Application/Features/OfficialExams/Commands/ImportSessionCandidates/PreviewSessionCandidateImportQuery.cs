using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.ImportSessionCandidates;

public sealed record PreviewSessionCandidateImportQuery(
    Guid ActorUserId,
    Guid SessionId,
    string? Mode,
    IReadOnlyList<CandidateRosterImportRow> Rows)
    : IRequest<Result<CandidateRosterImportPreviewResponse>>;
