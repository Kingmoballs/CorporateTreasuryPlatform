namespace Treasury.Domain.Entities;

/*
 * Each successful refresh consumes one record and creates
 * a replacement. Keeping consumed hashes enables replay
 * detection without storing the bearer secret itself.
 */
public class AuthenticationRefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AuthenticationSessionId
        { get; set; }

    public AuthenticationSession
        AuthenticationSession { get; set; } =
            null!;

    public string TokenHash { get; set; } =
        string.Empty;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public Guid? ReplacedByTokenId { get; set; }

    public AuthenticationRefreshToken?
        ReplacedByToken { get; set; }

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();
}
