namespace Treasury.Application.DTOs.Audit;

public class AuditLogResponseDto
{
    public Guid Id { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? ActorEmail { get; set; }

    public string? ActorRole { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public string? EntityReference { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string? BeforeValuesJson { get; set; }

    public string? AfterValuesJson { get; set; }

    public string? MetadataJson { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime OccurredAtUtc { get; set; }
}