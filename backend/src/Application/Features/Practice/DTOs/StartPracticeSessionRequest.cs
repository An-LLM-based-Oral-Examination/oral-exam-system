using System;

namespace OralExamination.Application.Features.Practice.DTOs;

public record StartPracticeSessionRequest(
    Guid CourseId,
    string Difficulty,
    int QuestionCount,
    bool IsFullSession = false,
    string? Topic = null,
    Guid? StudentId = null
);
