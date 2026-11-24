using System;
using System.Collections.Generic;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Enums;

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
    public bool IsSampleSet { get; set; }
    public bool IsActive { get; set; }
    public string OverlapSeverity { get; set; } = "NONE";
    public decimal YellowOverlapRatio { get; set; }
    public int RedOverlapQuestionCount { get; set; }
    public int YellowOverlapQuestionCount { get; set; }
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
    public string OverlapSeverity { get; set; } = "NONE";
    public string BankCurationStatus { get; set; } = OralExamination.Domain.Enums.BankCurationStatus.BankReady;
    public Guid? CloId { get; set; }
}

public sealed class CreateInlineExamQuestionDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? SampleAnswer { get; set; }
    public List<string> KeyPoints { get; set; } = [];
    public string Difficulty { get; set; } = QuestionDifficulty.Medium;
    public string BloomLevel { get; set; } = OralExamination.Domain.Enums.BloomLevel.Understand;
    public Guid? CloId { get; set; }
    public Guid? RubricId { get; set; }
    public RubricDraftDto? Rubric { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageDescription { get; set; }
}

public sealed class CreateExamSetRequest
{
    public Guid StructureId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? SessionId { get; set; }
    public string SetCode { get; set; } = string.Empty;
    public List<Guid> QuestionIds { get; set; } = [];
    public bool IsSampleSet { get; set; }
    public List<CreateInlineExamQuestionDto>? InlineManualQuestions { get; set; }
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

public sealed class ReturnExamSetForRevisionRequest
{
    public string ReviewNotes { get; set; } = string.Empty;
}

public sealed class ReturnExamSetForRevisionResultDto
{
    public Guid ExamSetId { get; set; }
    public string SetCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ReviewNotes { get; set; } = string.Empty;
    public DateTime ReturnedAt { get; set; }
}

public sealed class AvailableExamSetQuestionDto
{
    public Guid QuestionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string BloomLevel { get; set; } = string.Empty;
    public Guid? CloId { get; set; }
    public string? CloCode { get; set; }
    public Guid RubricId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public string BankCurationStatus { get; set; } = OralExamination.Domain.Enums.BankCurationStatus.BankReady;
    public bool IsOwnQuestion { get; set; }
    public bool IsYellowPendingByOtherLecturer { get; set; }
    public string? YellowWarningLabel { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageDescription { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AvailableExamSetQuestionsResponseDto
{
    public Guid StructureId { get; set; }
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public int RequiredSetCount { get; set; }
    public int YellowThresholdPercent { get; set; } = 70;
    public int TotalAvailable { get; set; }
    public int YellowCount { get; set; }
    public List<AvailableExamSetQuestionDto> Items { get; set; } = [];
}

public sealed class BankCurationSimilarityMatchDto
{
    public Guid ComparedQuestionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string BloomLevel { get; set; } = string.Empty;
    public string BankCurationStatus { get; set; } = string.Empty;
    public bool IsFromExistingBank { get; set; }
    public List<string> SourceSetCodes { get; set; } = [];
    public decimal SimilarityScore { get; set; }
    public decimal SimilarityPercent { get; set; }
    public string Band { get; set; } = string.Empty;
}

public sealed class BankCurationCandidateDto
{
    public Guid QuestionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string BloomLevel { get; set; } = string.Empty;
    public Guid? CloId { get; set; }
    public string? CloCode { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public string BankCurationStatus { get; set; } = OralExamination.Domain.Enums.BankCurationStatus.PendingCuration;
    public Guid? SubmittedBy { get; set; }
    public string? SubmittedByName { get; set; }
    public List<string> SourceSetCodes { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public decimal MaxSimilarityScore { get; set; }
    public decimal MaxSimilarityPercent { get; set; }
    public string HighestBand { get; set; } = "CLEAR";
    public List<BankCurationSimilarityMatchDto> Matches { get; set; } = [];
}

public sealed class BankCurationCandidatesResponseDto
{
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public int TotalPendingCount { get; set; }
    public int RedCount { get; set; }
    public int YellowCount { get; set; }
    public int ClearCount { get; set; }
    public List<BankCurationCandidateDto> Items { get; set; } = [];
}

public sealed class CurateExamQuestionBankRequest
{
    public List<Guid> QuestionIds { get; set; } = [];
    public string Decision { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class CurateExamQuestionBankResultDto
{
    public Guid CourseId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
    public List<Guid> QuestionIds { get; set; } = [];
    public DateTime CuratedAt { get; set; }
}
