namespace OralExamination.Application.Features.AcademicCatalog.DTOs;

public sealed record CourseCatalogItemDto
{
    public Guid CourseId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Credits { get; init; }
    public Guid? AcademicProgramId { get; init; }
    public string? AcademicProgramCode { get; init; }
    public bool IsCourseActive { get; init; }
    public Guid? CourseOfferingId { get; init; }
    public bool IsSelected { get; init; }
}

public sealed record SyncCourseOfferingsDto
{
    public Guid SemesterId { get; init; }
    public int CreatedCount { get; init; }
    public int EnabledCount { get; init; }
    public int DisabledCount { get; init; }
    public IReadOnlyList<CourseCatalogItemDto> Courses { get; init; } = Array.Empty<CourseCatalogItemDto>();
}

public sealed record AcademicProgramDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public Guid? DepartmentHeadUserId { get; init; }
    public string? DepartmentHeadName { get; init; }
    public bool IsActive { get; init; }
    public int CoursesCount { get; init; }
}
