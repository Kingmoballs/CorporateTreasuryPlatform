namespace Treasury.Application.DTOs.TreasuryAlerts;

public class TreasuryAlertQueryDto
{
    public string? Status { get; set; }

    public string? AlertType { get; set; }

    public string? Severity { get; set; }

    public Guid? AccountId { get; set; }

    public string? Currency { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}