namespace OralExamination.Application.Features.OfficialExams.DTOs;

public sealed record CreateAudioUploadAuthorizationRequest(
    string? ExpectedSha256Base64 = null);

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
    int TimeSpentSeconds,
    string? InitialVoiceTranscript = null,
    bool IsManuallyEdited = false);

public sealed record SubmitOfficialExamRequest(
    IReadOnlyList<OfficialExamAnswerSubmissionRequest>? Answers,
    bool FinalizePerTurn = false);

public sealed record SubmitOfficialExamResponse(
    Guid TicketId,
    string Status,
    DateTime SubmittedAt,
    int SubmissionCount,
    bool IsKioskLocked = true);

public sealed record CreateQuestionAudioUploadAuthorizationRequest(
    int FollowUpIndex = 0,
    string? ExpectedSha256Base64 = null);

public sealed record SubmitOfficialQuestionAnswerRequest(
    int FollowUpIndex,
    Guid? ParentSubmissionId,
    string AudioObjectKey,
    string AudioHashSha256,
    string? InitialVoiceTranscript = null,
    string? TranscriptWhisper = null,
    string? TimestampsWhisper = null,
    int TimeSpentSeconds = 0,
    bool IsManuallyEdited = false);

public sealed record SubmitOfficialQuestionAnswerResponse(
    Guid SubmissionId,
    Guid TicketId,
    Guid ExamQuestionId,
    int FollowUpIndex,
    string AudioObjectKey,
    string AudioHashSha256,
    string Status,
    bool CanRequestFollowUp);

public sealed record OfficialExamNextFollowUpResponse(
    bool HasMoreFollowUp,
    int FollowUpIndex,
    int MaxFollowUpQuestions,
    Guid ParentSubmissionId,
    Guid QuestionId,
    string? FollowUpPrompt);

public sealed record OfficialExamSubmissionStatusResponse(
    Guid TicketId,
    string Status,
    DateTime? SubmittedAt,
    int TotalSubmissions,
    int PendingSubmissions,
    int GradedSubmissions,
    int FailedSubmissions);
