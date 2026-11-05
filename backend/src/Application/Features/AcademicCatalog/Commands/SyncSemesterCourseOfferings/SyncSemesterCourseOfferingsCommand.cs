using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.DTOs;

namespace OralExamination.Application.Features.AcademicCatalog.Commands.SyncSemesterCourseOfferings;

public sealed record SyncSemesterCourseOfferingsCommand(
    Guid ActorUserId,
    Guid SemesterId,
    IReadOnlyCollection<Guid> SelectedCourseIds) : IRequest<Result<SyncCourseOfferingsDto>>;
