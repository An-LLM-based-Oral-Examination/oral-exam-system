using System;

namespace OralExamination.Application.Features.Courses.DTOs;

/// <summary>
/// DTO thông tin môn học phục vụ hiển thị danh sách môn học trên Student Portal Dashboard và quản trị môn học.
/// </summary>
public sealed class CourseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int Credits { get; set; }
    public Guid SemesterId { get; set; }
    public Guid? DepartmentHeadUserId { get; set; }
    public int TranscriptBufferSeconds { get; set; }
    public int MaxFollowUpQuestions { get; set; }
    public int MaxMockExamsPerDay { get; set; } = 3;
    public int MockExamDurationMinutes { get; set; } = 30;
    public int MockExamTotalQuestions { get; set; } = 5;
    public string MockExamBloomDistribution { get; set; } = "{\"Remember\": 1, \"Understand\": 2, \"Apply\": 2}";
    public bool HasFollowUp { get; set; }
    public string ExamInputMode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
