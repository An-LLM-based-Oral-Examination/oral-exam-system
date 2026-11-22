namespace OralExamination.Domain.Enums;

public static class OfficialExamAttemptType
{
    public const string FinalExam = "FE";
    public const string RetakeExam = "RE";

    public static readonly string[] All = { FinalExam, RetakeExam };

    public static bool IsValid(string? value) =>
        value is FinalExam or RetakeExam;
}
