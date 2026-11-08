using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.InClassShifts.DTOs;

public sealed record InClassExamShiftResponse(
    Guid Id,
    Guid ClassId,
    string ClassCode,
    string ClassName,
    Guid LecturerId,
    string? LecturerName,
    string ShiftName,
    DateTime StartTime,
    DateTime EndTime,
    string Status,
    bool IsLocked,
    DateTime? FinalizedAt,
    DateTime CreatedAt,
    int ParticipantsCount,
    int SubmissionsCount,
    string? Description = null,
    string QuestionPoolMode = "ALL_COURSE_BANK",
    IReadOnlyList<Guid>? SelectedQuestionIds = null,
    int TotalQuestionsPerStudent = 1,
    bool HasFollowUp = false,
    int MaxFollowUpQuestions = 2,
    bool IsAutoPublishGrades = true,
    int? ExamDurationMinutes = null,
    string? MyParticipationStatus = null,
    DateTime? MyJoinedAt = null,
    DateTime? MyExamEndsAt = null,
    int? MyRemainingSeconds = null
);

public sealed record InClassShiftDetailResponse(
    Guid Id,
    Guid ClassId,
    string ClassCode,
    string ClassName,
    Guid LecturerId,
    string? LecturerName,
    string ShiftName,
    DateTime StartTime,
    DateTime EndTime,
    string Status,
    bool IsLocked,
    DateTime? FinalizedAt,
    DateTime CreatedAt,
    IReadOnlyList<InClassShiftParticipantResponse> Participants,
    IReadOnlyList<InClassExamSubmissionResponse> Submissions,
    string? Description = null,
    string QuestionPoolMode = "ALL_COURSE_BANK",
    IReadOnlyList<Guid>? SelectedQuestionIds = null,
    int TotalQuestionsPerStudent = 1,
    bool HasFollowUp = false,
    int MaxFollowUpQuestions = 2,
    bool IsAutoPublishGrades = true,
    int? ExamDurationMinutes = null
);

public sealed record InClassAssignedQuestionDto(
    Guid QuestionId,
    string Title,
    string Content,
    string Difficulty,
    string BloomLevel
);

public sealed record InClassShiftParticipantResponse(
    Guid Id,
    Guid ShiftId,
    Guid StudentId,
    string? StudentName,
    string? StudentEmail,
    string Status,
    DateTime JoinedAt,
    Guid? AssignedQuestionId = null,
    InClassAssignedQuestionDto? AssignedQuestion = null,
    IReadOnlyList<Guid>? AssignedQuestionIds = null,
    IReadOnlyList<InClassAssignedQuestionDto>? AssignedQuestions = null,
    decimal? TotalAiScore = null,
    decimal? TotalFinalScore = null,
    string? StudentCode = null
);

public sealed record InClassExamSubmissionResponse(
    Guid Id,
    Guid ShiftId,
    Guid StudentId,
    string? StudentName,
    string? StudentEmail,
    Guid QuestionId,
    string Transcript,
    decimal? AiScore,
    string? AiFeedback,
    decimal? FinalScore,
    string? LecturerNotes,
    string Status,
    DateTime SubmittedAt,
    DateTime? GradedAt,
    string? QuestionTitle = null,
    string? QuestionContent = null,
    bool IsFollowUp = false,
    Guid? ParentSubmissionId = null,
    int FollowUpIndex = 0,
    string? FollowUpPrompt = null,
    decimal? AiConfidenceScore = null,
    string? StudentCode = null
);

public sealed record ActiveShiftPasswordResponse(
    Guid ShiftId,
    string CurrentPassword,
    int RemainingSeconds,
    DateTime ValidFrom,
    DateTime ValidTo
);

public sealed record JoinInClassShiftResponse(
    Guid ShiftId,
    Guid StudentId,
    string ShiftName,
    string Status,
    DateTime JoinedAt,
    Guid? AssignedQuestionId = null,
    InClassAssignedQuestionDto? AssignedQuestion = null,
    IReadOnlyList<Guid>? AssignedQuestionIds = null,
    IReadOnlyList<InClassAssignedQuestionDto>? AssignedQuestions = null,
    int TotalQuestionsPerStudent = 1,
    bool HasFollowUp = false,
    int MaxFollowUpQuestions = 0,
    DateTime? ExamEndsAt = null,
    int RemainingSeconds = 0,
    bool TimedOut = false
);

public sealed record SubmitInClassAnswerResponse(
    Guid SubmissionId,
    Guid ShiftId,
    Guid StudentId,
    Guid QuestionId,
    string Status,
    DateTime SubmittedAt,
    bool IsFollowUp = false,
    Guid? ParentSubmissionId = null,
    int FollowUpIndex = 0
);

public sealed record FinalizeInClassGradesResponse(
    Guid ShiftId,
    string ShiftName,
    string Status,
    bool IsLocked,
    DateTime? FinalizedAt
);
