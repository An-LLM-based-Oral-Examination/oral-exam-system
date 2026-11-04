using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Commands.CreateCourse;

/// <summary>
/// DTO yêu cầu tạo mới môn học từ phía Quản trị viên (Admin).
/// </summary>
public sealed record CreateCourseRequestDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Credits { get; init; } = 3;
    public string? Description { get; init; }
    public Guid? DepartmentHeadUserId { get; init; }
    public Guid? SemesterId { get; init; }
    public int MaxMockExamsPerDay { get; init; } = 3;
    public int MockExamDurationMinutes { get; init; } = 20;
    public int MockExamTotalQuestions { get; init; } = 5;
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// CQRS Command tạo mới môn học trong chuỗi phân cấp học thuật R5 (Admin).
/// </summary>
public sealed record CreateCourseCommand(
    string Code,
    string Name,
    int Credits = 3,
    string? Description = null,
    Guid? DepartmentHeadUserId = null,
    int MaxMockExamsPerDay = 3,
    int MockExamDurationMinutes = 20,
    int MockExamTotalQuestions = 5,
    bool IsActive = true,
    Guid? SemesterId = null) : IRequest<Result<CourseDto>>;
