using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.Practice.DTOs;

public record StartPracticeSessionRequest(
    Guid CourseId,
    string? Difficulty = null,
    int? QuestionCount = null,
    bool IsFullSession = false,
    string? Topic = null,
    Guid? StudentId = null,
    List<string>? Difficulties = null
);
