namespace Treasury.Application.DTOs.Auth;

public class AuthenticationSecurityEventResponseDto
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public Guid? AuthenticationSessionId
        { get; set; }

    public string EventType { get; set; } =
        string.Empty;

    public string Outcome { get; set; } =
        string.Empty;

    public string? ReasonCode { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime OccurredAtUtc { get; set; }
}
