using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.ImportExamRoster;

public sealed record PreviewExamRosterImportQuery(
    Guid ActorUserId,
    Guid ShiftId,
    IReadOnlyList<BulkExamTicketImportRow> Rows) : IRequest<Result<BulkExamTicketImportPreview>>;

public sealed record CommitExamRosterImportCommand(
    Guid ActorUserId,
    Guid ShiftId,
    IReadOnlyList<BulkExamTicketImportRow> Rows) : IRequest<Result<BulkExamTicketImportCommitResult>>;
