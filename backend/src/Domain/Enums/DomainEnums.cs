namespace OralExamination.Domain.Enums;

public static class ApprovalStatus
{
    public const string Draft = "DRAFT";
    public const string SubmittedForReview = "SUBMITTED_FOR_REVIEW";
    public const string Approved = "APPROVED";
    public const string NeedsRevision = "NEEDS_REVISION";
    public const string Rejected = "REJECTED";

    public static readonly string[] All = { Draft, SubmittedForReview, Approved, NeedsRevision, Rejected };
}

public static class BloomLevel
{
    public const string Remember = "Remember";
    public const string Understand = "Understand";
    public const string Apply = "Apply";
    public const string Analyze = "Analyze";
    public const string Evaluate = "Evaluate";
    public const string Create = "Create";

    public static readonly string[] All = { Remember, Understand, Apply, Analyze, Evaluate, Create };
}

public static class QuestionDifficulty
{
    public const string Easy = "easy";
    public const string Medium = "medium";
    public const string Hard = "hard";

    public static readonly string[] All = { Easy, Medium, Hard };
}

public static class QuestionSource
{
    public const string Manual = "manual";
    public const string FlmApi = "flm_api";

    public static readonly string[] All = { Manual, FlmApi };
}

public static class UsageScope
{
    public const string Practice = "practice";
    public const string Exam = "exam";
    public const string Shared = "shared";

    public static readonly string[] All = { Practice, Exam, Shared };
}

public static class UserRole
{
    public const string Admin = "admin";
    public const string DepartmentHead = "department_head";
    public const string Lecturer = "lecturer";
    public const string Proctor = "proctor";
    public const string Student = "student";

    public static readonly string[] All = { Admin, DepartmentHead, Lecturer, Proctor, Student };
}

public static class AppealStatus
{
    public const string Pending = "PENDING";
    public const string InReview = "IN_REVIEW";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";

    public static readonly string[] All = { Pending, InReview, Approved, Rejected, Cancelled };
}

public static class ExamTicketStatus
{
    public const string Scheduled = "SCHEDULED";
    public const string InProgress = "IN_PROGRESS";
    public const string Submitted = "SUBMITTED";
    public const string AiGraded = "AI_GRADED";
    public const string Audited = "AUDITED";
    public const string Published = "PUBLISHED";
    public const string Locked = "LOCKED";

    public static readonly string[] All = { Scheduled, InProgress, Submitted, AiGraded, Audited, Published, Locked };
}

public static class ExamInputMode
{
    public const string VoiceOnly = "VoiceOnly";
    public const string VoiceWithTranscriptEdit = "VoiceWithTranscriptEdit";
    public const string VoiceAndTextInput = "VoiceAndTextInput";

    public static readonly string[] All = { VoiceOnly, VoiceWithTranscriptEdit, VoiceAndTextInput };
}

