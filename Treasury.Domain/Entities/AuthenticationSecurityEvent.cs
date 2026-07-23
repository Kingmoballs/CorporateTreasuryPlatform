namespace Treasury.Domain.Entities;

/*
 * Append-only evidence for authentication and credential
 * security activity. OrganizationId is nullable because
 * failed unauthenticated attempts may have no tenant.
 */
public class AuthenticationSecurityEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? OrganizationId { get; set; }

    public Organization? Organization
        { get; set; }

    public Guid? UserId { get; set; }

    public User? User { get; set; }

    public Guid? AuthenticationSessionId
        { get; set; }

    public AuthenticationSession?
        AuthenticationSession { get; set; }

    public string EventType { get; set; } =
        string.Empty;

    public string Outcome { get; set; } =
        string.Empty;

    public string? ReasonCode { get; set; }

    public string? IdentifierHash { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime OccurredAtUtc { get; set; } =
        DateTime.UtcNow;
}
