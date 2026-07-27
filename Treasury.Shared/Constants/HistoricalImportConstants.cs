namespace Treasury.Shared.Constants;

public static class HistoricalImportModes
{
    public const string HistoricalTransactions =
        "HistoricalTransactions";

    public const string CutoverOpeningBalances =
        "CutoverOpeningBalances";

    public static bool IsSupported(string? value)
    {
        return value is HistoricalTransactions or
            CutoverOpeningBalances;
    }
}

public static class HistoricalImportStatuses
{
    public const string Validated = "Validated";

    public const string ValidationFailed =
        "ValidationFailed";

    public const string PendingApproval =
        "PendingApproval";

    public const string Approved = "Approved";

    public const string Rejected = "Rejected";

    public const string Committed = "Committed";
}

public static class HistoricalTransactionDirections
{
    public const string Credit = "Credit";

    public const string Debit = "Debit";
}
