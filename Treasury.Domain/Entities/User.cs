namespace Treasury.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime? EmailVerifiedAtUtc
        { get; set; }

    public DateTime? PasswordChangedAtUtc
        { get; set; }

    public Guid SecurityStamp { get; set; } =
        Guid.NewGuid();

    public int FailedLoginAttempts { get; set; }

    public DateTime? LoginFailureWindowStartedAtUtc
        { get; set; }

    public DateTime? LastFailedLoginAtUtc
        { get; set; }

    public DateTime? LoginLockoutEndUtc
        { get; set; }

    public string? ProtectedTotpSecret
        { get; set; }

    public DateTime? MfaEnrollmentStartedAtUtc
        { get; set; }

    public DateTime? MfaEnabledAtUtc
        { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

    /*
     * RoleId remains for backward compatibility while
     * OrganizationMemberships becomes the source of the
     * user's organization-specific roles.
     */
    public ICollection<OrganizationMembership>
        OrganizationMemberships { get; set; } =
            new List<OrganizationMembership>();
}
