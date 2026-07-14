namespace Treasury.Application.DTOs.Transactions;

public class TreasuryActivitySummaryDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public DateTime ActivityFromUtc { get; set; }

    public DateTime ActivityToUtc { get; set; }

    public int TotalTransactionCount { get; set; }

    public int CompletedTransactionCount { get; set; }

    public int CurrencyCount { get; set; }

    public IReadOnlyList<CurrencyTreasuryActivitySummaryDto>
        ByCurrency { get; set; }
        = Array.Empty<CurrencyTreasuryActivitySummaryDto>();
}