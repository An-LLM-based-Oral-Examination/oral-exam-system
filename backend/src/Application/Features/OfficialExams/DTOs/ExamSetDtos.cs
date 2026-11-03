using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.OfficialExams.DTOs;

public sealed class ExamSetDto
{
    public Guid Id { get; set; }
    public Guid StructureId { get; set; }
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public Guid? CreatedBy { get; set; }
    public string SetCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? ApprovedBy { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public DateTime GeneratedAt { get; set; }
    public bool IsActive { get; set; }
    public List<ExamSetQuestionItemDto> Questions { get; set; } = [];
}

public sealed class ExamSetQuestionItemDto
{
    public Guid QuestionId { get; set; }
    public int OrderIndex { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string BloomLevel { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public bool IsDuplicate { get; set; }
    public List<string> DuplicateInSets { get; set; } = [];
}

public sealed class CreateExamSetRequest
{
    public Guid StructureId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? SessionId { get; set; }
    public string SetCode { get; set; } = string.Empty;
    public List<Guid> QuestionIds { get; set; } = [];
}

public sealed class SubmitExamSetRequest
{
    public Guid ExamSetId { get; set; }
}

public sealed class ApproveExamSetRequest
{
    public Guid ExamSetId { get; set; }
    public string? ReviewNotes { get; set; }
}

public sealed class ApproveExamSetResultDto
{
    public Guid ExamSetId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int QuestionsCascadedCount { get; set; }
    public bool HasDuplicates { get; set; }
    public int DuplicateQuestionCount { get; set; }
    public List<string> DuplicateWarnings { get; set; } = [];
}

public sealed class HandOverExamSetRequest
{
    public Guid ExamSetId { get; set; }
}
