namespace Treasury.Domain.Entities;

/*
 * A one-time password-recovery credential. Only the
 * SHA-256 hash is persisted; the bearer token is delivered
 * to the user by email.
 */
public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string TokenHash { get; set; } =
        string.Empty;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();
}
