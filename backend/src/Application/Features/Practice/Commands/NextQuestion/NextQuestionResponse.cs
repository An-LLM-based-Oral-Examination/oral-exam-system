using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.Practice.Commands.NextQuestion;

public sealed class NextQuestionResponse
{
    public bool HasMoreQuestions { get; set; }
    public string? Message { get; set; }
    public NextQuestionDto? Question { get; set; }
}

public sealed class NextQuestionDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = null!;
    public string Difficulty { get; set; } = null!;
    public int QuestionOrder { get; set; }
    public List<string> RubricCriteria { get; set; } = new();
}
