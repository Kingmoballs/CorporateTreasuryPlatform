namespace Treasury.Application.DTOs.TreasuryAlerts;

public class TreasuryAlertSummaryQueryDto
{
    public Guid? AccountId { get; set; }

    public string? Currency { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}