namespace Treasury.Domain.Entities;

/*
 * A high-entropy, single-use MFA recovery credential.
 * Only its SHA-256 hash is stored.
 */
public class MfaRecoveryCode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string CodeHash { get; set; } =
        string.Empty;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime? ConsumedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();
}
