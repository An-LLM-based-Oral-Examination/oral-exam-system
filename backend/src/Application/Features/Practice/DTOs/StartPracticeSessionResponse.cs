using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.Practice.DTOs;

public record StartPracticeSessionResponse(
    Guid SessionId,
    int TranscriptBufferSeconds,
    List<PracticeQuestionDto> Questions
);

public record PracticeQuestionDto(
    Guid QuestionId,
    string Content,
    List<string> RubricCriteria
);
