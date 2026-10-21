using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.QuestionBank.DTOs;

public sealed record UploadQuestionImageResponseDto(
    string ImageUrl,
    string ObjectKey,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Description
);

public sealed record DifficultyDistributionDto(
    int Easy,
    int Medium,
    int Hard
);

public sealed record RubricCriterionDraftDto(
    string CriterionName,
    decimal MaxScore,
    decimal Weight,
    string BloomLevel,
    string Description,
    int OrderIndex = 1
);

public sealed record RubricDraftDto(
    string Name,
    string? Description,
    decimal TotalMaxScore,
    List<RubricCriterionDraftDto> Criteria
);

public sealed record GeneratedQuestionDraftDto(
    string TempId,
    string CloCode,
    string Title,
    string Content,
    string Difficulty,
    string BloomLevel,
    string Source,
    string UsageScope,
    string ModelAnswer,
    List<string> KeyPoints,
    bool HasFollowUp,
    string? FollowUpPrompt,
    RubricDraftDto Rubric
);

public sealed record GenerateQuestionsFromFlmResponseDto(
    Guid CourseId,
    string CourseCode,
    int GeneratedCount,
    string Source,
    List<GeneratedQuestionDraftDto> Questions
);

public sealed record BatchSubmitQuestionsForReviewResponseDto(
    Guid CourseId,
    int SubmittedCount,
    string ApprovalStatus,
    Guid SubmittedBy,
    DateTime SubmittedAt,
    string Message
);

public sealed record QuestionReviewDecisionResponseDto(
    Guid QuestionId,
    string ApprovalStatus,
    Guid ReviewedBy,
    DateTime ReviewedAt,
    string ReviewNotes,
    string Message
);

public sealed record QuestionSummaryDto(
    Guid Id,
    Guid CourseId,
    string CourseCode,
    string Title,
    string Difficulty,
    string BloomLevel,
    string Source,
    string ApprovalStatus,
    Guid? SubmittedBy,
    Guid? ApprovedBy,
    DateTime CreatedAt,
    decimal TotalRubricScore,
    string? ImageUrl = null,
    string? ImageDescription = null
);

public sealed record QuestionDetailDto(
    Guid Id,
    Guid CourseId,
    string CourseCode,
    Guid RubricId,
    string Title,
    string Content,
    string? SampleAnswer,
    List<string> KeyPoints,
    string Difficulty,
    string BloomLevel,
    string Source,
    string ApprovalStatus,
    Guid? SubmittedBy,
    string? SubmittedByName,
    Guid? ApprovedBy,
    string? ApprovedByName,
    string? ReviewNotes,
    bool IsActive,
    DateTime CreatedAt,
    RubricDetailDto Rubric,
    string? ImageUrl = null,
    string? ImageDescription = null
);

public sealed record RubricDetailDto(
    Guid Id,
    string Name,
    string? Description,
    decimal TotalMaxScore,
    List<RubricCriterionDetailDto> Criteria
);

public sealed record RubricCriterionDetailDto(
    Guid Id,
    string CriterionName,
    string? Description,
    decimal MaxScore,
    decimal Weight,
    string? BloomLevel,
    int OrderIndex
);

public sealed record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);

public sealed record GenerateQuestionsFromFlmRequestDto(
    Guid CourseId,
    List<string> SelectedCloCodes,
    List<string> Topics,
    int NumberOfQuestions,
    string UsageScope,
    List<string> TargetBloomLevels,
    DifficultyDistributionDto DifficultyDistribution,
    Guid? RequesterUserId = null
);

public sealed record CreateDraftQuestionRequestDto(
    Guid CourseId,
    string Title,
    string Content,
    string? SampleAnswer,
    List<string> KeyPoints,
    string Difficulty,
    string BloomLevel,
    string? Source,
    string? UsageScope,
    bool HasFollowUp,
    string? FollowUpPrompt,
    RubricDraftDto? Rubric = null,
    Guid? LecturerId = null,
    Guid? RubricId = null,
    Guid? ApprovedBy = null,
    string? ImageUrl = null,
    string? ImageDescription = null
);

public sealed record BatchSubmitQuestionsForReviewRequestDto(
    Guid CourseId,
    List<Guid> QuestionIds,
    string? SubmissionNotes,
    Guid? LecturerId = null
);

public sealed record QuestionReviewDecisionRequestDto(
    string Decision,
    string ReviewNotes,
    string? TargetUsageScope,
    Guid? ReviewerId = null
);

public sealed record FlmCloDto(
    string CloCode,
    string Description,
    string TargetBloomLevel
);

public sealed record FlmTopicDto(
    string TopicId,
    int SessionNumber,
    string TopicName,
    List<string> MappedClos
);

public sealed record FlmPaginationMetadataDto(
    int PageNumber,
    int PageSize,
    int TotalTopics,
    int TotalPages
);

public sealed record FlmSyllabusResponseDto(
    Guid CourseId,
    string CourseCode,
    string CourseName,
    string SyllabusCode,
    int TotalCredits,
    List<FlmCloDto> Clos,
    List<FlmTopicDto> Topics,
    FlmPaginationMetadataDto Pagination
);

public sealed record CreateRubricRequestDto(
    Guid CourseId,
    string Name,
    string? Description,
    List<RubricCriterionDraftDto> Criteria
);

public sealed record CreateRubricResponseDto(
    Guid Id,
    Guid CourseId,
    string Name,
    string? Description,
    decimal TotalMaxScore,
    int CriteriaCount,
    DateTime CreatedAt,
    List<RubricCriterionDetailDto> Criteria
);

public sealed record ApprovedQuestionItemDto(
    string Title,
    string Content,
    string Difficulty,
    string BloomLevel,
    string UsageScope,
    string Source,
    string ModelAnswer,
    List<string> KeyPoints,
    bool HasFollowUp,
    string? FollowUpPrompt,
    RubricDraftDto Rubric,
    string? ImageUrl = null,
    string? ImageDescription = null
);

public sealed record ApprovedQuestionResultDto(
    Guid QuestionId,
    Guid RubricId,
    string Title,
    string UsageScope,
    string Source,
    string Status,
    Guid ApprovedBy,
    DateTime CreatedAt
);

public sealed record BatchApproveQuestionsRequestDto(
    Guid CourseId,
    List<ApprovedQuestionItemDto> Questions,
    Guid? ApproverId = null
);

public sealed record BatchApproveQuestionsResponseDto(
    Guid CourseId,
    int TotalApproved,
    List<ApprovedQuestionResultDto> ApprovedQuestions
);
