using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task<bool> TryCreate(
        PasswordResetToken token,
        DateTime cooldownThresholdUtc);

    Task<PasswordResetToken?> GetByTokenHash(
        string tokenHash);

    Task Revoke(
        Guid tokenId,
        DateTime revokedAtUtc);

    Task<bool> ConsumeAndChangePassword(
        Guid tokenId,
        Guid userId,
        string passwordHash,
        Guid securityStamp,
        DateTime changedAtUtc);
}
