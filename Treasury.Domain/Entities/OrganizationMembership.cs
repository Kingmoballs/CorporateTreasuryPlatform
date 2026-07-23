namespace Treasury.Domain.Entities;

/*
 * Connects a user to an organization and records the role
 * that the user holds within that organization.
 */
public class OrganizationMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public Organization Organization { get; set; } =
        null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; }

    public DateTime JoinedAtUtc { get; set; } =
        DateTime.UtcNow;

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();
}
