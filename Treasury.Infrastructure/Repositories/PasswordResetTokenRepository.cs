using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class PasswordResetTokenRepository
    : IPasswordResetTokenRepository
{
    private readonly TreasuryDbContext _context;

    public PasswordResetTokenRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TryCreate(
        PasswordResetToken token,
        DateTime cooldownThresholdUtc)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        /*
         * Serialize requests per user. This makes the
         * cooldown and active-token replacement atomic
         * across API instances.
         */
        await _context.Database
            .ExecuteSqlInterpolatedAsync(
                $"""
                SELECT 1
                FROM "Users"
                WHERE "Id" = {token.UserId}
                FOR UPDATE
                """);

        var cooldownIsActive =
            await _context.PasswordResetTokens
                .AsNoTracking()
                .AnyAsync(existing =>
                    existing.UserId ==
                        token.UserId &&
                    existing.ConsumedAtUtc == null &&
                    existing.RevokedAtUtc == null &&
                    existing.CreatedAtUtc >
                        cooldownThresholdUtc);

        if (cooldownIsActive)
        {
            await transaction.RollbackAsync();

            return false;
        }

        await _context.PasswordResetTokens
            .Where(existing =>
                existing.UserId == token.UserId &&
                existing.ConsumedAtUtc == null &&
                existing.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(
                        existing =>
                            existing.RevokedAtUtc,
                        token.CreatedAtUtc)
                    .SetProperty(
                        existing =>
                            existing.ConcurrencyToken,
                        Guid.NewGuid()));

        await _context.PasswordResetTokens
            .AddAsync(token);

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return true;
    }

    public Task<PasswordResetToken?>
        GetByTokenHash(string tokenHash)
    {
        /*
         * The unique hash is the authorization secret for
         * this intentionally unscoped lookup.
         */
        return _context.PasswordResetTokens
            .AsNoTracking()
            .Include(token => token.User)
            .FirstOrDefaultAsync(token =>
                token.TokenHash == tokenHash);
    }

    public Task Revoke(
        Guid tokenId,
        DateTime revokedAtUtc)
    {
        return _context.PasswordResetTokens
            .Where(token =>
                token.Id == tokenId &&
                token.ConsumedAtUtc == null &&
                token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(
                        token =>
                            token.RevokedAtUtc,
                        revokedAtUtc)
                    .SetProperty(
                        token =>
                            token.ConcurrencyToken,
                        Guid.NewGuid()));
    }

    public async Task<bool>
        ConsumeAndChangePassword(
            Guid tokenId,
            Guid userId,
            string passwordHash,
            Guid securityStamp,
            DateTime changedAtUtc)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        var consumedCount =
            await _context.PasswordResetTokens
                .Where(token =>
                    token.Id == tokenId &&
                    token.UserId == userId &&
                    token.ConsumedAtUtc == null &&
                    token.RevokedAtUtc == null &&
                    token.ExpiresAtUtc >
                        changedAtUtc)
                .ExecuteUpdateAsync(setters =>
                    setters
                        .SetProperty(
                            token =>
                                token.ConsumedAtUtc,
                            changedAtUtc)
                        .SetProperty(
                            token =>
                                token.ConcurrencyToken,
                            Guid.NewGuid()));

        if (consumedCount != 1)
        {
            await transaction.RollbackAsync();

            return false;
        }

        var changedUserCount =
            await _context.Users
                .Where(user =>
                    user.Id == userId &&
                    user.IsActive &&
                    user.EmailVerifiedAtUtc != null)
                .ExecuteUpdateAsync(setters =>
                    setters
                        .SetProperty(
                            user =>
                                user.PasswordHash,
                            passwordHash)
                        .SetProperty(
                            user =>
                                user.PasswordChangedAtUtc,
                            changedAtUtc)
                        .SetProperty(
                            user =>
                                user.SecurityStamp,
                            securityStamp));

        if (changedUserCount != 1)
        {
            await transaction.RollbackAsync();

            return false;
        }

        await _context.PasswordResetTokens
            .Where(token =>
                token.UserId == userId &&
                token.ConsumedAtUtc == null &&
                token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(
                        token =>
                            token.RevokedAtUtc,
                        changedAtUtc)
                    .SetProperty(
                        token =>
                            token.ConcurrencyToken,
                        Guid.NewGuid()));

        var sessionIds =
            _context.AuthenticationSessions
                .Where(session =>
                    session.UserId == userId)
                .Select(session => session.Id);

        await _context.AuthenticationSessions
            .Where(session =>
                session.UserId == userId &&
                session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(
                        session =>
                            session.RevokedAtUtc,
                        changedAtUtc)
                    .SetProperty(
                        session =>
                            session.RevocationReason,
                        "Password changed.")
                    .SetProperty(
                        session =>
                            session.ConcurrencyToken,
                        Guid.NewGuid()));

        await _context.AuthenticationRefreshTokens
            .Where(token =>
                sessionIds.Contains(
                    token.AuthenticationSessionId) &&
                token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(
                        token =>
                            token.RevokedAtUtc,
                        changedAtUtc)
                    .SetProperty(
                        token =>
                            token.ConcurrencyToken,
                        Guid.NewGuid()));

        await transaction.CommitAsync();

        return true;
    }
}
