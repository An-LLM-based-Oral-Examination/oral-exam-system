namespace OralExamination.Application.Features.OfficialExams.DTOs;

public sealed record OfficialExamAudioUploadAuthorizationResponse(
    string ObjectKey,
    string FileName,
    string UploadUrl,
    DateTime ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);

public sealed record SealOfficialExamAudioRequest(string? ObjectKey, string? Sha256);

public sealed record OfficialExamAudioSealResponse(
    Guid TicketId,
    string ObjectKey,
    string Sha256,
    long ContentLength,
    DateTime VerifiedAt);

public sealed record OfficialExamAnswerSubmissionRequest(
    Guid ExamQuestionId,
    string? TranscriptWhisper,
    string? TimestampsWhisper,
    int TimeSpentSeconds);

public sealed record SubmitOfficialExamRequest(
    IReadOnlyList<OfficialExamAnswerSubmissionRequest>? Answers);

public sealed record SubmitOfficialExamResponse(
    Guid TicketId,
    string Status,
    DateTime SubmittedAt,
    int SubmissionCount);

public sealed record OfficialExamSubmissionStatusResponse(
    Guid TicketId,
    string Status,
    DateTime? SubmittedAt,
    int TotalSubmissions,
    int PendingSubmissions,
    int GradedSubmissions,
    int FailedSubmissions);
