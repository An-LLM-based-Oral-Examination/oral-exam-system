using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.DTOs;

namespace OralExamination.Application.Features.AcademicCatalog.Queries.GetAcademicPrograms;

public sealed record GetAcademicProgramsQuery(bool? IsActive = null)
    : IRequest<Result<IReadOnlyList<AcademicProgramDto>>>;
