using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Commands.UpdateCourse;

/// <summary>
/// DTO yêu cầu cập nhật thông tin môn học từ phía Quản trị viên (Admin).
/// </summary>
public sealed record UpdateCourseRequestDto
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
/// CQRS Command cập nhật thông tin môn học trong chuỗi phân cấp học thuật R5 (Admin).
/// </summary>
public sealed record UpdateCourseCommand(
    Guid CourseId,
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
