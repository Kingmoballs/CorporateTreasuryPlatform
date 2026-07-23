namespace Treasury.Domain.Entities;

/*
 * Short-lived continuation of a successful password
 * check. The bearer token is never stored in plaintext.
 */
public class MfaLoginChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid OrganizationId { get; set; }

    public Organization Organization
        { get; set; } = null!;

    public Guid OrganizationMembershipId
        { get; set; }

    public OrganizationMembership
        OrganizationMembership { get; set; } =
            null!;

    public string TokenHash { get; set; } =
        string.Empty;

    public Guid SecurityStamp { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public int FailedAttempts { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime? LockedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();
}
