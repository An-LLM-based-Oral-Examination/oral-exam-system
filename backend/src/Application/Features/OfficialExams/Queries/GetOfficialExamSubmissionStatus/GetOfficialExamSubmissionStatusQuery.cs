using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSubmissionStatus;

public sealed record GetOfficialExamSubmissionStatusQuery(Guid ActorUserId, Guid TicketId)
    : IRequest<Result<OfficialExamSubmissionStatusResponse>>;
