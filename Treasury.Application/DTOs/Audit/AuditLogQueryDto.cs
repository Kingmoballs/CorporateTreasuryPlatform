namespace Treasury.Application.DTOs.Audit;

public class AuditLogQueryDto
{
    public Guid? ActorUserId { get; set; }

    public string? Action { get; set; }

    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}