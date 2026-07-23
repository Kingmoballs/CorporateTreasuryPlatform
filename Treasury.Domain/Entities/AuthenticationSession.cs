namespace Treasury.Domain.Entities;

/*
 * Server-side state for one signed-in device or client.
 * It is intentionally not globally query-filtered because
 * refresh requests arrive without an access-token tenant
 * claim. Services always scope administrative operations.
 */
public class AuthenticationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid OrganizationId { get; set; }

    public Organization Organization { get; set; } =
        null!;

    public Guid OrganizationMembershipId
        { get; set; }

    public OrganizationMembership
        OrganizationMembership { get; set; } =
            null!;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime LastActivityAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string AuthenticationMethod
        { get; set; } = "password";

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public Guid SecurityStamp { get; set; }

    public string? RevocationReason { get; set; }

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();

    public ICollection<AuthenticationRefreshToken>
        RefreshTokens { get; set; } =
            new List<AuthenticationRefreshToken>();
}
