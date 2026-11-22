namespace OralExamination.Domain.Enums;

public static class BankCurationStatus
{
    public const string BankReady = "BANK_READY";
    public const string PendingCuration = "PENDING_CURATION";
    public const string CuratedIntoBank = "CURATED_INTO_BANK";
    public const string ExcludedFromBank = "EXCLUDED_FROM_BANK";

    public static readonly string[] All =
    {
        BankReady,
        PendingCuration,
        CuratedIntoBank,
        ExcludedFromBank
    };

    public static bool IsValid(string? value) =>
        value is BankReady or PendingCuration or CuratedIntoBank or ExcludedFromBank;
}
