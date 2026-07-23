namespace Treasury.Domain.Entities;

/*
 * Invitations are organization-scoped, but are not an
 * IOrganizationOwnedEntity. An unauthenticated recipient
 * must be able to resolve a single invitation by its
 * high-entropy token hash. All administrator queries are
 * explicitly restricted to the current organization.
 */
public class UserInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public Organization Organization { get; set; } =
        null!;

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } =
        string.Empty;

    public string LastName { get; set; } =
        string.Empty;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public string TokenHash { get; set; } =
        string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? AcceptedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public Guid InvitedByUserId { get; set; }

    public User InvitedByUser { get; set; } =
        null!;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();
}
