namespace Treasury.Application.DTOs.Auth;

public class RecordAuthenticationSecurityEventDto
{
    public Guid? OrganizationId { get; set; }

    public Guid? UserId { get; set; }

    public Guid? AuthenticationSessionId
        { get; set; }

    public string EventType { get; set; } =
        string.Empty;

    public string Outcome { get; set; } =
        string.Empty;

    public string? ReasonCode { get; set; }

    public string? Identifier { get; set; }

    public object? Metadata { get; set; }
}
