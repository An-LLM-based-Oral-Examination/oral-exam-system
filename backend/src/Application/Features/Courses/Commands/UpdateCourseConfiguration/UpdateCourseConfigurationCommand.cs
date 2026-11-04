using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;

/// <summary>
/// CQRS Command cập nhật cấu hình thời gian đệm, số câu hỏi phụ và tham số thi thử cho môn học.
/// </summary>
public sealed record UpdateCourseConfigurationCommand(
    Guid CourseId,
    int TranscriptBufferSeconds,
    int MaxFollowUpQuestions,
    bool? HasFollowUp,
    string? ExamInputMode = null
) : IRequest<Result<CourseConfigurationDto>>
{
    public int? MaxMockExamsPerDay { get; init; }
    public int? MockExamDurationMinutes { get; init; }
    public int? MockExamTotalQuestions { get; init; }
    public string? MockExamBloomDistribution { get; init; }
    public string? ActorRole { get; init; }
}
