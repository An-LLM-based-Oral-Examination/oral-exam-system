using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;

/// <summary>
/// CQRS Command cập nhật cấu hình thời gian đệm và số câu hỏi phụ cho môn học.
/// </summary>
public sealed record UpdateCourseConfigurationCommand(
    Guid CourseId,
    int TranscriptBufferSeconds,
    int MaxFollowUpQuestions,
    bool? HasFollowUp,
    string? ExamInputMode = null
) : IRequest<Result<CourseConfigurationDto>>;
