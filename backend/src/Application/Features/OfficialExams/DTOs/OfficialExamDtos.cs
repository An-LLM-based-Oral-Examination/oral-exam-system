using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OralExamination.Application.Features.OfficialExams.DTOs;

/// <summary>
/// DTO chứa đầy đủ bằng chứng kiểm toán (Evidence Panel) cho từng thí sinh trong ca thi phòng Lab (MF-04).
/// Bao gồm Audio URL trên R2, mã băm SHA-256, bản bóc băng Whisper, điểm AI, độ tin cậy,
/// cờ phát hiện nghi ngờ AI Doubt Guard, chuỗi suy luận CoT và điểm chốt chính thức.
/// </summary>
public sealed class AuditEvidenceItemDto
{
    public Guid TicketId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AudioR2Url { get; set; }
    public string? AudioHashSha256 { get; set; }
    public string? TranscriptWhisper { get; set; }
    public string? TimestampsWhisper { get; set; }
    public decimal? AiScore { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public bool IsSuspicious { get; set; }
    public string? SuspiciousReason { get; set; }
    public string? AiChainOfThought { get; set; }
    public string? CotTrace
    {
        get => AiChainOfThought;
        set => AiChainOfThought = value;
    }
    public decimal? FinalScore { get; set; }
    public bool IsLocked { get; set; }
    public Guid? SubmissionId { get; set; }
    public string? OverrideReason { get; set; }
    public Guid? QuestionId { get; set; }
    public string? QuestionTitle { get; set; }
    public string? QuestionContent { get; set; }
    public string? QuestionImageUrl { get; set; }
    public string? QuestionImageDescription { get; set; }
    public List<AuditCriterionEvidenceItemDto>? CriterionDetails { get; set; }
    public List<AuditMainSubmissionEvidenceItemDto>? MainSubmissions { get; set; }
    public List<AuditFollowUpEvidenceItemDto>? FollowUpItems { get; set; }
}

public sealed class AuditCriterionEvidenceItemDto
{
    public Guid CriterionId { get; set; }
    public string CriterionName { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public decimal? AiScore { get; set; }
    public decimal? AuditedScore { get; set; }
    public string? Comment { get; set; }
}

public sealed class AuditMainSubmissionEvidenceItemDto
{
    public Guid SubmissionId { get; set; }
    public Guid ExamQuestionId { get; set; }
    public int OrderIndex { get; set; }
    public string? QuestionTitle { get; set; }
    public string? QuestionContent { get; set; }
    public string? QuestionImageUrl { get; set; }
    public string? QuestionImageDescription { get; set; }
    public string? AudioR2Url { get; set; }
    public string? AudioHashSha256 { get; set; }
    public string? TranscriptWhisper { get; set; }
    public string? TimestampsWhisper { get; set; }
    public decimal? AiScore { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public bool IsSuspicious { get; set; }
    public string? SuspiciousReason { get; set; }
    public decimal? FinalScore { get; set; }
    public string? CotTrace { get; set; }
    public List<AuditCriterionEvidenceItemDto>? CriterionDetails { get; set; }
}

public sealed record OfficialExamAudioPlaybackUrlResponse(
    Guid TicketId,
    Guid? SubmissionId,
    string DownloadUrl,
    string? AudioHashSha256,
    DateTime ExpiresAt);

public sealed class AuditFollowUpEvidenceItemDto
{
    public Guid SubmissionId { get; set; }
    public Guid? ParentSubmissionId { get; set; }
    public Guid ExamQuestionId { get; set; }
    public int FollowUpIndex { get; set; }
    public string? FollowUpPrompt { get; set; }
    public string? TranscriptWhisper { get; set; }
    public string? AudioR2Url { get; set; }
    public string? AudioHashSha256 { get; set; }
    public decimal? AiScore { get; set; }
    public decimal? FinalScore { get; set; }
    public string? CotTrace { get; set; }
}

/// <summary>
/// DTO phản hồi kết quả công bố điểm toàn bộ ca thi và kích hoạt One-Way Lock.
/// </summary>
public sealed class PublishGradesResponseDto
{
    public Guid ShiftId { get; set; }
    public int TotalPublished { get; set; }
    public DateTime PublishedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class CreateOfficialExamSessionRequest
{
    public Guid CourseId { get; set; }
    public Guid? SemesterId { get; set; }
    public Guid? ExamStructureId { get; set; }
    public string? Title { get; set; }
    public DateOnly ExamDate { get; set; }
    public bool HasFollowUp { get; set; }
    public int MaxFollowUpQuestions { get; set; } = OfficialExamRules.DefaultFollowUpQuestions;
    public string? ExamInputMode { get; set; }
    public int TranscriptBufferSeconds { get; set; } = 60;
    public string? ExamAttemptType { get; set; } = OralExamination.Domain.Enums.OfficialExamAttemptType.FinalExam;
}

public sealed class UpdateOfficialExamSessionRequest
{
    public Guid? ExamStructureId { get; set; }
    public string? Title { get; set; }
    public DateOnly ExamDate { get; set; }
    public string? ExamInputMode { get; set; }
    public int TranscriptBufferSeconds { get; set; } = 60;
}

public sealed record OfficialExamSessionResponse(
    Guid Id,
    Guid CourseId,
    Guid? ExamStructureId,
    string Title,
    DateOnly ExamDate,
    bool HasFollowUp,
    int MaxFollowUpQuestions,
    string ExamInputMode,
    int TranscriptBufferSeconds,
    string Status,
    Guid? SemesterId = null,
    string ExamAttemptType = OralExamination.Domain.Enums.OfficialExamAttemptType.FinalExam,
    string? CourseCode = null,
    string? CourseName = null,
    int CandidateCount = 0,
    int AllocatedCandidateCount = 0);

public sealed record SemesterCourseAttemptSessionSummaryDto(
    Guid SessionId,
    string ExamAttemptType,
    string Title,
    string Status,
    int CandidateCount,
    int AllocatedCount);

public sealed record SemesterEligibleExamCourseDto(
    Guid CourseId,
    string CourseCode,
    string CourseName,
    Guid SemesterId,
    string SemesterCode,
    int ActiveClassCount,
    int EligibleStudentCount,
    int MaxStudentsPerRoom,
    int RecommendedRoomCount,
    Guid? ActiveExamStructureId,
    string? ActiveExamStructureName,
    int HandedOverExamSetCount,
    bool HasFollowUp,
    int MaxFollowUpQuestions,
    Guid? ExistingSessionId,
    string? ExistingSessionTitle,
    string? ExistingSessionStatus,
    SemesterCourseAttemptSessionSummaryDto? FeSession = null,
    SemesterCourseAttemptSessionSummaryDto? ReSession = null);

public sealed class BatchCreateOfficialExamSessionCourseItemDto
{
    public Guid CourseId { get; set; }
    public string? Title { get; set; }
    public string? ExamAttemptType { get; set; }
}

public sealed class BatchCreateOfficialExamSessionsRequest
{
    public DateOnly ExamDate { get; set; }
    public string? ExamAttemptType { get; set; } = OralExamination.Domain.Enums.OfficialExamAttemptType.FinalExam;
    public string? ExamInputMode { get; set; } = OralExamination.Domain.Enums.ExamInputMode.VoiceWithTranscriptEdit;
    public int TranscriptBufferSeconds { get; set; } = 60;
    public bool SkipExisting { get; set; } = false;
    public IReadOnlyList<BatchCreateOfficialExamSessionCourseItemDto> Courses { get; set; } = Array.Empty<BatchCreateOfficialExamSessionCourseItemDto>();
}

public sealed record BatchCreateOfficialExamSessionsResponse(
    Guid SemesterId,
    int CreatedCount,
    IReadOnlyList<OfficialExamSessionResponse> Sessions);

public sealed record OfficialExamEnrollmentEstimateDto(
    Guid SessionId,
    Guid CourseId,
    string CourseCode,
    string CourseName,
    Guid SemesterId,
    string SemesterCode,
    int EligibleStudentCount,
    int AllocatedStudentCount,
    int UnallocatedStudentCount,
    int MaxStudentsPerRoom,
    int RecommendedTotalRooms,
    int RecommendedAdditionalRooms,
    int CurrentShiftRoomCount,
    int CurrentTotalCapacity,
    int CurrentAvailableSeats,
    int HandedOverExamSetCount,
    string ExamAttemptType = OralExamination.Domain.Enums.OfficialExamAttemptType.FinalExam,
    string CandidateSource = "IMPORTED_CANDIDATES",
    bool HasImportedCandidates = false,
    int ImportedCandidateCount = 0,
    int SemesterEnrolledCount = 0);

public sealed record CandidateRosterImportRow(
    int SourceRow,
    string StudentCode,
    string? FullName = null,
    string? ClassCode = null);

public sealed record CandidateRosterImportIssue(
    int? SourceRow,
    string Message);

public sealed record CandidateRosterImportPreviewRowDto(
    int SourceRow,
    string StudentCode,
    Guid? StudentId,
    string? FullName,
    string? Email,
    bool IsEnrolledInSemester,
    bool AlreadyInSession,
    bool IsValid,
    IReadOnlyList<string> Issues);

public sealed record CandidateRosterImportPreviewResponse(
    Guid SessionId,
    string ExamAttemptType,
    string Mode,
    int TotalRows,
    int ValidCount,
    int InvalidCount,
    int DuplicateCount,
    bool CanCommit,
    int ExistingCandidateCount,
    int ProjectedTotalCandidates,
    int MaxStudentsPerRoom,
    int RecommendedTotalRooms,
    IReadOnlyList<CandidateRosterImportIssue> Issues,
    IReadOnlyList<CandidateRosterImportPreviewRowDto> Rows);

public sealed record CandidateRosterImportCommitResponse(
    Guid SessionId,
    string ExamAttemptType,
    string Mode,
    int ImportedCount,
    int TotalCandidateCount,
    int RecommendedTotalRooms,
    DateTime ImportedAt);

public sealed record SessionCandidateItemDto(
    Guid StudentId,
    string StudentCode,
    string FullName,
    string Email,
    DateTime ImportedAt,
    bool IsAllocated,
    Guid? TicketId,
    Guid? ShiftId,
    string? RoomLab,
    int? SeatNumber,
    string? TicketStatus);

public sealed record SessionCandidatesPageResponse(
    Guid SessionId,
    string ExamAttemptType,
    int TotalCount,
    int AllocatedCount,
    int UnallocatedCount,
    int PageNumber,
    int PageSize,
    IReadOnlyList<SessionCandidateItemDto> Items);

public sealed record CandidateRosterTemplateFile(
    byte[] Contents,
    string ContentType,
    string FileName);

public sealed class BatchOpenExamShiftRoomDto
{
    public string? RoomLab { get; set; }
    public int MaxSeats { get; set; } = OfficialExamRules.DefaultShiftCapacity;
    public Guid? ProctorUserId { get; set; }
    public Guid? ExaminerUserId { get; set; }
}

public sealed class BatchOpenExamShiftsRequest
{
    public string? ShiftGroupName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public IReadOnlyList<BatchOpenExamShiftRoomDto> Rooms { get; set; } = Array.Empty<BatchOpenExamShiftRoomDto>();
}

public sealed record BatchOpenExamShiftsResponse(
    Guid SessionId,
    string ShiftGroupName,
    int CreatedRoomCount,
    int TotalCapacityCreated,
    IReadOnlyList<ExamShiftResponse> Shifts);

public sealed class AutoAllocateSessionStudentsRequest
{
    public IReadOnlyList<Guid>? ShiftIds { get; set; }
    public bool AssignHandedOverExamSets { get; set; } = false;
    public bool DryRun { get; set; } = false;
}

public sealed record AutoAllocatedRoomDto(
    Guid ShiftId,
    string RoomLab,
    int MaxSeats,
    int AssignedBefore,
    int AssignedNow,
    int TotalAfter);

public sealed record AutoAllocatedShiftGroupDto(
    DateTime StartTime,
    DateTime EndTime,
    bool IsFinalBalancedShift,
    int TotalAssignedInGroup,
    IReadOnlyList<AutoAllocatedRoomDto> Rooms);

public sealed record AutoAllocateSessionStudentsResponse(
    Guid SessionId,
    bool DryRun,
    int TotalEligibleStudents,
    int PreviouslyAllocatedCount,
    int NewlyAllocatedCount,
    IReadOnlyList<AutoAllocatedShiftGroupDto> ShiftGroups);

public sealed class OpenExamShiftRequest
{
    public Guid SessionId { get; set; }
    public string? ShiftName { get; set; }
    public string? RoomLab { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Guid ProctorUserId { get; set; }
    public Guid ExaminerUserId { get; set; }
    public int MaxSeats { get; set; } = OfficialExamRules.DefaultShiftCapacity;
}

public sealed record ExamShiftResponse(
    Guid Id,
    Guid SessionId,
    string ShiftName,
    string RoomLab,
    DateTime StartTime,
    DateTime EndTime,
    Guid? ProctorUserId,
    string Status,
    Guid? ExaminerUserId = null,
    string? ExaminerName = null,
    string? ProctorName = null,
    int MaxSeats = OfficialExamRules.DefaultShiftCapacity,
    bool IsExamPublished = false,
    bool IsGradesPublished = false,
    DateTime? GradesPublishedAt = null,
    string? CertificateHash = null,
    string ExamAttemptType = OralExamination.Domain.Enums.OfficialExamAttemptType.FinalExam,
    int AssignedTicketsCount = 0,
    int RemainingSeats = OfficialExamRules.DefaultShiftCapacity);

public sealed class ConfigureOfficialExamFollowUpRequest
{
    public bool HasFollowUp { get; set; }
    public int MaxFollowUpQuestions { get; set; } = OfficialExamRules.DefaultFollowUpQuestions;
}

public sealed class AssignExamTicketRequest
{
    public Guid ShiftId { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentCode { get; set; }
    public int SeatNumber { get; set; }
    public string? IpAddress { get; set; }
}

public sealed record WorkstationExamQuestionDto(
    Guid QuestionId,
    int OrderIndex,
    string Title,
    string Content,
    string BloomLevel,
    string Difficulty,
    string? ImageUrl = null,
    string? ImageDescription = null);

public sealed record ExamTicketResponse(
    Guid Id,
    Guid ShiftId,
    Guid StudentId,
    int SeatNumber,
    string? IpAddress,
    string Status,
    DateTime CheckedInAt,
    Guid? ExamSetId = null,
    string? ExamSetCode = null,
    string? DeviceId = null,
    DateTime? DeviceLockedAt = null,
    int DeviceUnlockCount = 0,
    string? ActiveSessionToken = null);

public sealed class CheckInExamTicketRequest
{
    public string? DeviceId { get; set; }
    public string? KioskVersion { get; set; }
}

public sealed class UnlockExamTicketDeviceRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed record UnlockExamTicketDeviceResponse(
    Guid TicketId,
    Guid ShiftId,
    string StudentCode,
    int SeatNumber,
    string? PreviousDeviceId,
    int DeviceUnlockCount,
    Guid UnlockedBy,
    DateTime UnlockedAt,
    string Reason);

public sealed record WorkstationFollowUpItemDto(
    Guid SubmissionId,
    Guid? ParentSubmissionId,
    Guid ExamQuestionId,
    int FollowUpIndex,
    string? FollowUpPrompt,
    string? GeneratedFollowUpPrompt,
    string? TranscriptWhisper,
    string? AudioObjectKey,
    string? AudioHashSha256);

/// <summary>One imported roster row. SourceRow is the human-facing spreadsheet/CSV row number.</summary>
public sealed record BulkExamTicketImportRow(
    int SourceRow,
    string StudentCode,
    int SeatNumber,
    string IpAddress);

public sealed record BulkExamTicketImportIssue(int? SourceRow, string Message);

public sealed record BulkExamTicketImportPreviewRow(
    int SourceRow,
    string StudentCode,
    string? FullName,
    int SeatNumber,
    string IpAddress,
    IReadOnlyList<string> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed record BulkExamTicketImportPreview(
    Guid ShiftId,
    int ValidRowCount,
    int AvailableHandedOverSetCount,
    IReadOnlyList<BulkExamTicketImportIssue> Issues,
    IReadOnlyList<string> ReservedExamSetCodes,
    IReadOnlyList<BulkExamTicketImportPreviewRow>? Rows = null)
{
    public bool CanCommit => Issues.Count == 0;
}

public sealed record BulkExamTicketImportCommitResult(
    Guid ShiftId,
    int CreatedTicketCount,
    IReadOnlyList<ExamTicketResponse> Tickets);

public sealed record StudentWorkstationTicketResponse(
    Guid Id,
    Guid ShiftId,
    string ShiftName,
    string RoomLab,
    DateTime StartTime,
    DateTime EndTime,
    string ShiftStatus,
    string SessionTitle,
    Guid StudentId,
    int SeatNumber,
    string Status,
    Guid? ExamSetId = null,
    string? ExamSetCode = null,
    string? ExamInputMode = null,
    int TranscriptBufferSeconds = 60,
    bool HasFollowUp = false,
    int MaxFollowUpQuestions = 2,
    IReadOnlyList<WorkstationExamQuestionDto>? Questions = null,
    bool IsExamPublished = false,
    bool IsKioskLocked = false,
    string? DeviceId = null,
    DateTime? DeviceLockedAt = null,
    int DeviceUnlockCount = 0,
    int ViolationCount = 0,
    int MaxViolations = OfficialExamRules.MaxAllowedWindowBlurs,
    int LockThreshold = OfficialExamRules.WindowBlurLockThreshold,
    int RemainingStrikes = OfficialExamRules.WindowBlurLockThreshold,
    DateTime? ServerNowUtc = null,
    string ExamAttemptType = OralExamination.Domain.Enums.OfficialExamAttemptType.FinalExam,
    IReadOnlyList<WorkstationFollowUpItemDto>? FollowUps = null);

public sealed record OfficialExamTicketStateResponse(
    Guid TicketId,
    Guid ShiftId,
    string Status,
    bool IsExamPublished,
    bool IsKioskLocked,
    int ViolationCount,
    int MaxViolations,
    int LockThreshold,
    int RemainingStrikes,
    DateTime ServerNowUtc,
    string? DeviceId,
    DateTime? DeviceLockedAt,
    int DeviceUnlockCount,
    IReadOnlyList<WorkstationFollowUpItemDto>? FollowUps = null);

public sealed record ExamStructureDto(
    Guid Id,
    Guid CourseId,
    string CourseCode,
    Guid CreatedBy,
    string Name,
    int TotalQuestions,
    int DurationMinutes,
    string BloomDistribution,
    bool IsActive,
    DateTime CreatedAt,
    int RequiredSetCount = 1,
    string SlotDefinitions = "[]")
{
    public int EasyCount { get; init; }
    public int MediumCount { get; init; }
    public int HardCount { get; init; }
    public IReadOnlyList<Guid> CloIds { get; init; } = [];
}

public sealed record CreateExamStructureRequest(
    Guid CourseId,
    string Name,
    int TotalQuestions = 5,
    int DurationMinutes = 30,
    string? BloomDistribution = "{}",
    bool IsActive = true,
    int RequiredSetCount = 1,
    string? SlotDefinitions = null);

public sealed record UpdateExamStructureRequest(
    string Name,
    int TotalQuestions = 5,
    int DurationMinutes = 30,
    string? BloomDistribution = "{}",
    bool IsActive = true,
    int RequiredSetCount = 1,
    string? SlotDefinitions = null);
