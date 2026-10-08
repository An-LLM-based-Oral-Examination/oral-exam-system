using System;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Courses.DTOs;

/// <summary>
/// DTO chứa cấu hình động của môn học do Quản trị viên (Admin) hoặc Trưởng Bộ Môn thiết lập.
/// </summary>
public sealed class CourseConfigurationDto
{
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = null!;
    public string CourseName { get; set; } = null!;
    public int TranscriptBufferSeconds { get; set; }
    public int MaxFollowUpQuestions { get; set; }
    public bool HasFollowUp { get; set; }
    public string ExamInputMode { get; set; } = OralExamination.Domain.Enums.ExamInputMode.VoiceWithTranscriptEdit;
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO gửi từ client để cập nhật cấu hình động của môn học (Admin / Trưởng Bộ Môn).
/// </summary>
public sealed class UpdateCourseConfigRequestDto
{
    public int TranscriptBufferSeconds { get; set; }
    public int MaxFollowUpQuestions { get; set; }
    public bool? HasFollowUp { get; set; }
    public string? ExamInputMode { get; set; }
}

