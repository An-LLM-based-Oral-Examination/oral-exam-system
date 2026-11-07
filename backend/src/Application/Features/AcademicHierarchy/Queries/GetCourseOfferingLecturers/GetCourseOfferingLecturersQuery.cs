using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetCourseOfferingLecturers;

public sealed record GetCourseOfferingLecturersQuery(
    Guid CourseOfferingId,
    bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<CourseLecturerDto>>>;
