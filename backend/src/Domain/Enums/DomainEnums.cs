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

    public const string Progressive = "progressive";

    public static readonly string[] All = { Easy, Medium, Hard, Progressive };
}

public static class PracticeDifficulty
{
    public const string Easy = "easy";
    public const string Medium = "medium";
    public const string Hard = "hard";
    public const string Progressive = "progressive";

    public static readonly string[] All = { Easy, Medium, Hard, Progressive };
}

public static class QuestionSource
{
    public const string Manual = "manual";
    public const string AiGenerated = "ai_generated";
    public const string FlmApi = "flm_api"; // Deprecated: Kept for backwards compatibility

    public static readonly string[] All = { Manual, AiGenerated, FlmApi };
}

public static class UsageScope
{
    public const string Practice = "practice";
    public const string Exam = "exam";

    public static readonly string[] All = { Practice, Exam };
}

public static class UserRole
{
    public const string Admin = "admin";
    public const string DepartmentHead = "department_head";
    public const string ExaminationOfficer = "examination_officer";
    public const string Lecturer = "lecturer";
    public const string Proctor = "proctor";
    public const string Student = "student";

    public static readonly string[] All = { Admin, DepartmentHead, ExaminationOfficer, Lecturer, Proctor, Student };
}

public static class ExamSetStatus
{
    public const string NeedsRevision = "NEEDS_REVISION";
    public const string Draft = "DRAFT";
    public const string SubmittedForReview = "SUBMITTED_FOR_REVIEW";
    public const string Official = "OFFICIAL";
    public const string HandedOver = "HANDED_OVER";
    public const string Rejected = "REJECTED";

    public static readonly string[] All = { Draft, SubmittedForReview, Official, HandedOver, Rejected, NeedsRevision };
}

public static class AntiCheatEventType
{
    public const string TabSwitch = "TAB_SWITCH";
    public const string WindowBlur = "WINDOW_BLUR";
    public const string FullscreenExit = "FULLSCREEN_EXIT";
    public const string MultipleFaces = "MULTIPLE_FACES";
    public const string VoiceDetected = "VOICE_DETECTED";
    public const string DeviceDisconnection = "DEVICE_DISCONNECTION";
    public const string DisallowedProcess = "DISALLOWED_PROCESS";
    public const string MultiMonitor = "MULTI_MONITOR";
    public const string VmDetected = "VM_DETECTED";
    public const string RemoteControl = "REMOTE_CONTROL";
    public const string AudioTamper = "AUDIO_TAMPER";
    public const string HeartbeatTimeout = "HEARTBEAT_TIMEOUT";
    public const string ProctorReportedViolation = "PROCTOR_REPORTED_VIOLATION";
    public const string TabSwitchOrBlur = "TAB_SWITCH_OR_BLUR";
    public const string UnauthorizedDevice = "UNAUTHORIZED_DEVICE";
    public const string IdentityMismatch = "IDENTITY_MISMATCH";

    public static readonly string[] All =
    {
        TabSwitch,
        WindowBlur,
        FullscreenExit,
        MultipleFaces,
        VoiceDetected,
        DeviceDisconnection,
        DisallowedProcess,
        MultiMonitor,
        VmDetected,
        RemoteControl,
        AudioTamper,
        HeartbeatTimeout
    };

    public static readonly string[] ProctorAllowed =
    {
        TabSwitch,
        WindowBlur,
        FullscreenExit,
        MultipleFaces,
        VoiceDetected,
        DeviceDisconnection,
        DisallowedProcess,
        MultiMonitor,
        VmDetected,
        RemoteControl,
        AudioTamper,
        HeartbeatTimeout,
        ProctorReportedViolation,
        TabSwitchOrBlur,
        UnauthorizedDevice,
        IdentityMismatch
    };
}

public static class AntiCheatSeverity
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Critical = "critical";

    public static readonly string[] All = { Low, Medium, High, Critical };
}

public static class AppealStatus
{
    public const string SubmittedToExamOffice = "SUBMITTED_TO_EXAM_OFFICE";
    public const string AssignedToLecturer = "ASSIGNED_TO_LECTURER";
    public const string RegradedByLecturer = "REGRADED_BY_LECTURER";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";

    // Source compatibility only. Persisted values follow the mandatory five-step workflow above.
    public const string Pending = SubmittedToExamOffice;
    public const string InReview = AssignedToLecturer;

    public static readonly string[] All =
        { SubmittedToExamOffice, AssignedToLecturer, RegradedByLecturer, Approved, Rejected, Cancelled };

    public static bool IsOpen(string? status) =>
        status is SubmittedToExamOffice or AssignedToLecturer or RegradedByLecturer;
}

public static class StudentAcknowledgementStatus
{
    public const string Pending = "PENDING";
    public const string Acknowledged = "ACKNOWLEDGED";
    public const string Appealed = "APPEALED";

    public static readonly string[] All = { Pending, Acknowledged, Appealed };
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

    public static readonly string[] All = { VoiceOnly, VoiceWithTranscriptEdit };
}


public static class PracticeMode
{
    public const string PerQuestion = "per_question";
    public const string FullSession = "full_session";
    public static readonly string[] All = { PerQuestion, FullSession };
}

public static class PracticeSessionStatus
{
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Abandoned = "abandoned";
    public const string TimedOut = "timed_out";
    public static readonly string[] All = { InProgress, Completed, Abandoned, TimedOut };
}

public static class PracticeAnswerStatus
{
    public const string Pending = "pending";
    public const string Grading = "grading";
    public const string Graded = "graded";
    public const string Failed = "failed";
    public static readonly string[] All = { Pending, Grading, Graded, Failed };
}
