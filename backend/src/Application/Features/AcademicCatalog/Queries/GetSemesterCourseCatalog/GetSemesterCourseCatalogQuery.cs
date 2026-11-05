using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.DTOs;

namespace OralExamination.Application.Features.AcademicCatalog.Queries.GetSemesterCourseCatalog;

public sealed record GetSemesterCourseCatalogQuery(
    Guid SemesterId,
    bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<CourseCatalogItemDto>>>;
