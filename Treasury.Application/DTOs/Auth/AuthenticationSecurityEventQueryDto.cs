namespace Treasury.Application.DTOs.Auth;

public class AuthenticationSecurityEventQueryDto
{
    public Guid? UserId { get; set; }

    public string? EventType { get; set; }

    public string? Outcome { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}
