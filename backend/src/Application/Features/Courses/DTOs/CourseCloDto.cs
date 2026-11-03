using System;

namespace OralExamination.Application.Features.Courses.DTOs;

/// <summary>
/// DTO đại diện cho Chuẩn đầu ra môn học (CLO - Course Learning Outcome).
/// </summary>
public sealed class CourseCloDto
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string CloCode { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string BloomLevel { get; set; } = null!;
    public int OrderIndex { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CreateCourseCloRequest
{
    public string CloCode { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string BloomLevel { get; set; } = null!;
    public int OrderIndex { get; set; } = 1;
}

public sealed class UpdateCourseCloRequest
{
    public string CloCode { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string BloomLevel { get; set; } = null!;
    public int OrderIndex { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}
