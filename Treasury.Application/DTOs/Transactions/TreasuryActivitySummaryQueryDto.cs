namespace Treasury.Application.DTOs.Transactions;

public class TreasuryActivitySummaryQueryDto
{
    public string? Currency { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}