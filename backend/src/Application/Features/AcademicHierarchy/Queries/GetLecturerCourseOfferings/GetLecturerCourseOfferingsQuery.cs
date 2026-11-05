using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetLecturerCourseOfferings;

public sealed record GetLecturerCourseOfferingsQuery(
    Guid LecturerUserId,
    bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<LecturerCourseOfferingDto>>>;
