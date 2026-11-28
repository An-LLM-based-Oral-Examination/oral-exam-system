namespace OralExamination.Application.Features.OfficialExams;

public static class OfficialExamRules
{
    public const int MinSeat = 1;
    public const int MaxSeat = 40;
    public const int DefaultShiftCapacity = 24;
    public const int MinFollowUpQuestions = 1;
    public const int DefaultFollowUpQuestions = 2;
    public const int MaxFollowUpQuestions = 5;
    public const int MinTranscriptBufferSeconds = 10;
    public const int MaxTranscriptBufferSeconds = 300;
    public const int AudioUploadUrlLifetimeMinutes = 10;
    public const long MaxOfficialExamAudioBytes = 500L * 1024L * 1024L;
    public const long MaxCandidateRosterBytes = 2L * 1024L * 1024L;
    public const int MaxCandidateRosterRows = 2000;
    public const decimal SuspiciousConfidenceThreshold = 0.70m;
    public const decimal MaxTranscriptEditRatio = 0.35m;
    public const int MinUnlockDeviceReasonLength = 10;
    public const int MaxUnlockDeviceReasonLength = 500;
    public const int MaxAllowedWindowBlurs = 3;
    public const int WindowBlurLockThreshold = 4;
    public const int FollowUpGradingDeferralSeconds = 60;
    public const int FollowUpInFlightLeaseExtensionSeconds = 90;
}
