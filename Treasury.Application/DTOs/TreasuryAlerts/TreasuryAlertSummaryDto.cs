namespace Treasury.Application.DTOs.TreasuryAlerts;

public class TreasuryAlertSummaryDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public Guid? AccountId { get; set; }

    public string? Currency { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public int TotalAlertCount { get; set; }

    public int OpenAlertCount { get; set; }

    public int CriticalOpenAlertCount { get; set; }

    public int WarningOpenAlertCount { get; set; }

    public int InfoOpenAlertCount { get; set; }

    public int ResolvedAlertCount { get; set; }

    public int DismissedAlertCount { get; set; }

    public int CreatedTodayCount { get; set; }

    public List<TreasuryAlertSummaryBucketDto> ByStatus { get; set; } = new();

    public List<TreasuryAlertSummaryBucketDto> BySeverity { get; set; } = new();

    public List<TreasuryAlertSummaryBucketDto> ByAlertType { get; set; } = new();

    public List<TreasuryAlertSummaryBucketDto> BySourceModule { get; set; } = new();

    public List<TreasuryAlertResponseDto> LatestOpenAlerts { get; set; } = new();
}