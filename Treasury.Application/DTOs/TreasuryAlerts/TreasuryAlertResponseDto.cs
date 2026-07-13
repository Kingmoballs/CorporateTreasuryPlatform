namespace Treasury.Application.DTOs.TreasuryAlerts;

public class TreasuryAlertResponseDto
{
    public Guid Id { get; set; }

    public string AlertType { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid? AccountId { get; set; }

    public string? AccountName { get; set; }

    public string? Currency { get; set; }

    public string? SourceModule { get; set; }

    public string? SourceEntityType { get; set; }

    public Guid? SourceEntityId { get; set; }

    public string? SourceReference { get; set; }

    public string? MetadataJson { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid? ClosedByUserId { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public string? ClosureNote { get; set; }
}