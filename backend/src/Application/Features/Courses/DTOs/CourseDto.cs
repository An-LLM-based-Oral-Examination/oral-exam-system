using System;

namespace OralExamination.Application.Features.Courses.DTOs;

/// <summary>
/// DTO thông tin môn học phục vụ hiển thị danh sách môn học trên Student Portal Dashboard.
/// </summary>
public sealed class CourseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Credits { get; set; }
    public int TranscriptBufferSeconds { get; set; }
    public int MaxFollowUpQuestions { get; set; }
    public bool HasFollowUp { get; set; }
    public string ExamInputMode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
