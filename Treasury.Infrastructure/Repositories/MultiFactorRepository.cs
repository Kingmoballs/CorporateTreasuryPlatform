using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class MultiFactorRepository
    : IMultiFactorRepository
{
    private readonly TreasuryDbContext _context;

    public MultiFactorRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SetPendingEnrollment(
        Guid userId,
        Guid expectedSecurityStamp,
        string protectedSecret,
        DateTime startedAtUtc)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        var user = await LockAndReloadUser(userId);

        if (user is null ||
            user.SecurityStamp !=
                expectedSecurityStamp ||
            user.MfaEnabledAtUtc.HasValue)
        {
            await transaction.RollbackAsync();

            return false;
        }

        user.ProtectedTotpSecret =
            protectedSecret;

        user.MfaEnrollmentStartedAtUtc =
            startedAtUtc;

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return true;
    }

    public async Task<bool> Enable(
        Guid userId,
        Guid expectedSecurityStamp,
        DateTime enabledAtUtc,
        Guid newSecurityStamp,
        IReadOnlyCollection<MfaRecoveryCode>
            recoveryCodes)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        var user = await LockAndReloadUser(userId);

        if (user is null ||
            user.SecurityStamp !=
                expectedSecurityStamp ||
            user.MfaEnabledAtUtc.HasValue ||
            string.IsNullOrWhiteSpace(
                user.ProtectedTotpSecret))
        {
            await transaction.RollbackAsync();

            return false;
        }

        user.MfaEnabledAtUtc = enabledAtUtc;
        user.MfaEnrollmentStartedAtUtc = null;
        user.SecurityStamp = newSecurityStamp;

        await RevokeRecoveryCodes(
            userId,
            enabledAtUtc);

        await _context.MfaRecoveryCodes
            .AddRangeAsync(recoveryCodes);

        await _context.SaveChangesAsync();

        await RevokeChallenges(
            userId,
            enabledAtUtc);

        await RevokeSessions(
            userId,
            enabledAtUtc,
            "MFA enabled.");

        await transaction.CommitAsync();

        return true;
    }

    public async Task<bool> ReplaceRecoveryCodes(
        Guid userId,
        Guid expectedSecurityStamp,
        DateTime changedAtUtc,
        Guid newSecurityStamp,
        IReadOnlyCollection<MfaRecoveryCode>
            recoveryCodes)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        var user = await LockAndReloadUser(userId);

        if (user is null ||
            user.SecurityStamp !=
                expectedSecurityStamp ||
            !user.MfaEnabledAtUtc.HasValue ||
            string.IsNullOrWhiteSpace(
                user.ProtectedTotpSecret))
        {
            await transaction.RollbackAsync();

            return false;
        }

        user.SecurityStamp = newSecurityStamp;

        await RevokeRecoveryCodes(
            userId,
            changedAtUtc);

        await _context.MfaRecoveryCodes
            .AddRangeAsync(recoveryCodes);

        await _context.SaveChangesAsync();

        await RevokeChallenges(
            userId,
            changedAtUtc);

        await RevokeSessions(
            userId,
            changedAtUtc,
            "MFA recovery codes regenerated.");

        await transaction.CommitAsync();

        return true;
    }

    public async Task<bool> Disable(
        Guid userId,
        Guid expectedSecurityStamp,
        DateTime changedAtUtc,
        Guid newSecurityStamp)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        var user = await LockAndReloadUser(userId);

        if (user is null ||
            user.SecurityStamp !=
                expectedSecurityStamp ||
            !user.MfaEnabledAtUtc.HasValue)
        {
            await transaction.RollbackAsync();

            return false;
        }

        user.ProtectedTotpSecret = null;
        user.MfaEnrollmentStartedAtUtc = null;
        user.MfaEnabledAtUtc = null;
        user.SecurityStamp = newSecurityStamp;

        await RevokeRecoveryCodes(
            userId,
            changedAtUtc);

        await _context.SaveChangesAsync();

        await RevokeChallenges(
            userId,
            changedAtUtc);

        await RevokeSessions(
            userId,
            changedAtUtc,
            "MFA disabled.");

        await transaction.CommitAsync();

        return true;
    }

    public async Task<bool> TryCreateChallenge(
        MfaLoginChallenge challenge)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        var user =
            await LockAndReloadUser(
                challenge.UserId);

        if (user is null ||
            !user.IsActive ||
            !user.EmailVerifiedAtUtc.HasValue ||
            !user.MfaEnabledAtUtc.HasValue ||
            string.IsNullOrWhiteSpace(
                user.ProtectedTotpSecret) ||
            user.SecurityStamp !=
                challenge.SecurityStamp ||
            !await IsMembershipEligible(
                challenge))
        {
            await transaction.RollbackAsync();

            return false;
        }

        await RevokeChallenges(
            challenge.UserId,
            challenge.CreatedAtUtc);

        await _context.MfaLoginChallenges
            .AddAsync(challenge);

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return true;
    }

    public Task<MfaLoginChallenge?>
        GetChallengeByHash(string tokenHash)
    {
        return _context.MfaLoginChallenges
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(challenge =>
                challenge.User)
            .Include(challenge =>
                challenge.OrganizationMembership)
                .ThenInclude(membership =>
                    membership.Role)
            .Include(challenge =>
                challenge.OrganizationMembership)
                .ThenInclude(membership =>
                    membership.Organization)
            .FirstOrDefaultAsync(challenge =>
                challenge.TokenHash == tokenHash);
    }

    public async Task RecordFailedChallengeAttempt(
        Guid challengeId,
        DateTime failedAtUtc,
        int maximumAttempts)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        var challenge =
            await LockAndReloadChallenge(
                challengeId);

        if (challenge is null ||
            challenge.ConsumedAtUtc.HasValue ||
            challenge.RevokedAtUtc.HasValue ||
            challenge.LockedAtUtc.HasValue ||
            challenge.ExpiresAtUtc <= failedAtUtc)
        {
            await transaction.CommitAsync();

            return;
        }

        challenge.FailedAttempts =
            Math.Min(
                challenge.FailedAttempts + 1,
                maximumAttempts);

        if (challenge.FailedAttempts >=
            maximumAttempts)
        {
            challenge.LockedAtUtc =
                failedAtUtc;
        }

        challenge.ConcurrencyToken =
            Guid.NewGuid();

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();
    }

    public async Task<bool> ConsumeChallenge(
        Guid challengeId,
        Guid userId,
        DateTime consumedAtUtc,
        int maximumAttempts)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        var user = await LockAndReloadUser(userId);

        var challenge =
            await LockAndReloadChallenge(
                challengeId);

        if (!CanConsume(
                challenge,
                user,
                consumedAtUtc,
                maximumAttempts) ||
            !await IsMembershipEligible(
                challenge))
        {
            await transaction.RollbackAsync();

            return false;
        }

        challenge!.ConsumedAtUtc =
            consumedAtUtc;

        challenge.ConcurrencyToken =
            Guid.NewGuid();

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return true;
    }

    public async Task<bool>
        ConsumeChallengeWithRecoveryCode(
            Guid challengeId,
            Guid userId,
            string recoveryCodeHash,
            DateTime consumedAtUtc,
            int maximumAttempts)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        var user = await LockAndReloadUser(userId);

        var challenge =
            await LockAndReloadChallenge(
                challengeId);

        if (!CanConsume(
                challenge,
                user,
                consumedAtUtc,
                maximumAttempts) ||
            !await IsMembershipEligible(
                challenge))
        {
            await transaction.RollbackAsync();

            return false;
        }

        var recoveryCode =
            await _context.MfaRecoveryCodes
                .FirstOrDefaultAsync(code =>
                    code.UserId == userId &&
                    code.CodeHash ==
                        recoveryCodeHash);

        if (recoveryCode is null)
        {
            await transaction.RollbackAsync();

            return false;
        }

        await _context.Entry(recoveryCode)
            .ReloadAsync();

        if (recoveryCode.ConsumedAtUtc.HasValue ||
            recoveryCode.RevokedAtUtc.HasValue)
        {
            await transaction.RollbackAsync();

            return false;
        }

        recoveryCode.ConsumedAtUtc =
            consumedAtUtc;

        recoveryCode.ConcurrencyToken =
            Guid.NewGuid();

        challenge!.ConsumedAtUtc =
            consumedAtUtc;

        challenge.ConcurrencyToken =
            Guid.NewGuid();

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return true;
    }

    private async Task<User?> LockAndReloadUser(
        Guid userId)
    {
        await _context.Database
            .ExecuteSqlInterpolatedAsync(
                $"""
                SELECT 1
                FROM "Users"
                WHERE "Id" = {userId}
                FOR UPDATE
                """);

        var user =
            await _context.Users
                .FirstOrDefaultAsync(item =>
                    item.Id == userId);

        if (user is not null)
        {
            await _context.Entry(user)
                .ReloadAsync();
        }

        return user;
    }

    private async Task<MfaLoginChallenge?>
        LockAndReloadChallenge(Guid challengeId)
    {
        await _context.Database
            .ExecuteSqlInterpolatedAsync(
                $"""
                SELECT 1
                FROM "MfaLoginChallenges"
                WHERE "Id" = {challengeId}
                FOR UPDATE
                """);

        var challenge =
            await _context.MfaLoginChallenges
                .FirstOrDefaultAsync(item =>
                    item.Id == challengeId);

        if (challenge is not null)
        {
            await _context.Entry(challenge)
                .ReloadAsync();
        }

        return challenge;
    }

    private static bool CanConsume(
        MfaLoginChallenge? challenge,
        User? user,
        DateTime consumedAtUtc,
        int maximumAttempts)
    {
        return challenge is not null &&
            user is not null &&
            challenge.UserId == user.Id &&
            challenge.SecurityStamp ==
                user.SecurityStamp &&
            user.IsActive &&
            user.EmailVerifiedAtUtc.HasValue &&
            user.MfaEnabledAtUtc.HasValue &&
            !string.IsNullOrWhiteSpace(
                user.ProtectedTotpSecret) &&
            !challenge.ConsumedAtUtc.HasValue &&
            !challenge.RevokedAtUtc.HasValue &&
            !challenge.LockedAtUtc.HasValue &&
            challenge.FailedAttempts <
                maximumAttempts &&
            challenge.ExpiresAtUtc >
                consumedAtUtc;
    }

    private Task<bool> IsMembershipEligible(
        MfaLoginChallenge? challenge)
    {
        if (challenge is null)
        {
            return Task.FromResult(false);
        }

        return _context.OrganizationMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(membership =>
                membership.Id ==
                    challenge
                        .OrganizationMembershipId &&
                membership.UserId ==
                    challenge.UserId &&
                membership.OrganizationId ==
                    challenge.OrganizationId &&
                membership.IsActive &&
                membership.Organization.IsActive);
    }

    private Task RevokeRecoveryCodes(
        Guid userId,
        DateTime revokedAtUtc)
    {
        return _context.MfaRecoveryCodes
            .Where(code =>
                code.UserId == userId &&
                code.ConsumedAtUtc == null &&
                code.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(
                        code => code.RevokedAtUtc,
                        revokedAtUtc)
                    .SetProperty(
                        code =>
                            code.ConcurrencyToken,
                        Guid.NewGuid()));
    }

    private Task RevokeChallenges(
        Guid userId,
        DateTime revokedAtUtc)
    {
        return _context.MfaLoginChallenges
            .Where(challenge =>
                challenge.UserId == userId &&
                challenge.ConsumedAtUtc == null &&
                challenge.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(
                        challenge =>
                            challenge.RevokedAtUtc,
                        revokedAtUtc)
                    .SetProperty(
                        challenge =>
                            challenge.ConcurrencyToken,
                        Guid.NewGuid()));
    }

    private async Task RevokeSessions(
        Guid userId,
        DateTime revokedAtUtc,
        string reason)
    {
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
                        revokedAtUtc)
                    .SetProperty(
                        session =>
                            session.RevocationReason,
                        reason)
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
                        revokedAtUtc)
                    .SetProperty(
                        token =>
                            token.ConcurrencyToken,
                        Guid.NewGuid()));
    }
}
