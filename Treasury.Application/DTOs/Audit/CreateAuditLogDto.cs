namespace Treasury.Application.DTOs.Audit;

public class CreateAuditLogDto
{
    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public string? EntityReference { get; set; }

    public string Summary { get; set; } = string.Empty;

    public object? BeforeValues { get; set; }

    public object? AfterValues { get; set; }

    public object? Metadata { get; set; }
}