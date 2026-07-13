namespace Treasury.Application.DTOs.TreasuryAlerts;

public class CreateTreasuryAlertDto
{
    public string AlertType { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid? AccountId { get; set; }

    public string? Currency { get; set; }

    public string? SourceModule { get; set; }

    public string? SourceEntityType { get; set; }

    public Guid? SourceEntityId { get; set; }

    public string? SourceReference { get; set; }

    public object? Metadata { get; set; }
}