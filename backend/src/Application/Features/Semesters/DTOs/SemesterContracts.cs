using System;

namespace OralExamination.Application.Features.Semesters.DTOs;

public sealed record SemesterItemDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public bool IsActive { get; init; }
    public int CoursesCount { get; init; }
    public int ClassesCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record AcademicClassItemDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid CourseId { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public Guid SemesterId { get; init; }
    public string SemesterCode { get; init; } = string.Empty;
    public Guid? LecturerId { get; init; }
    public string? LecturerName { get; init; }
    public int MaxStudents { get; init; }
    public int EnrolledStudentsCount { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}
