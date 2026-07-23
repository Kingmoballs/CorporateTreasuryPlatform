using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class AuthenticationSessionRepository
    : IAuthenticationSessionRepository
{
    private readonly TreasuryDbContext _context;

    public AuthenticationSessionRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        AuthenticationSession session,
        AuthenticationRefreshToken refreshToken)
    {
        await _context.AuthenticationSessions
            .AddAsync(session);

        await _context.AuthenticationRefreshTokens
            .AddAsync(refreshToken);
    }

    public Task<AuthenticationRefreshToken?>
        GetRefreshTokenByHash(string tokenHash)
    {
        /*
         * The refresh-token hash is the authorization
         * secret for this unauthenticated lookup.
         */
        return _context.AuthenticationRefreshTokens
            .AsNoTracking()
            .Include(token =>
                token.AuthenticationSession)
                .ThenInclude(session =>
                    session.User)
            .Include(token =>
                token.AuthenticationSession)
                .ThenInclude(session =>
                    session.Organization)
            .Include(token =>
                token.AuthenticationSession)
                .ThenInclude(session =>
                    session
                        .OrganizationMembership)
                    .ThenInclude(membership =>
                        membership.Role)
            .Include(token =>
                token.AuthenticationSession)
                .ThenInclude(session =>
                    session
                        .OrganizationMembership)
                    .ThenInclude(membership =>
                        membership.Organization)
            .FirstOrDefaultAsync(token =>
                token.TokenHash == tokenHash);
    }

    public async Task<bool> RotateRefreshToken(
        Guid currentTokenId,
        AuthenticationRefreshToken replacement,
        DateTime consumedAtUtc)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        /*
         * Insert the replacement inside the transaction
         * before referencing it from the consumed token.
         * If another request wins the conditional update,
         * this insertion is rolled back.
         */
        await _context.AuthenticationRefreshTokens
            .AddAsync(replacement);

        await _context.SaveChangesAsync();

        var consumedCount =
            await _context
                .AuthenticationRefreshTokens
                .Where(token =>
                    token.Id == currentTokenId &&
                    token.ConsumedAtUtc == null &&
                    token.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters =>
                    setters
                        .SetProperty(
                            token =>
                                token.ConsumedAtUtc,
                            consumedAtUtc)
                        .SetProperty(
                            token =>
                                token
                                    .ReplacedByTokenId,
                            replacement.Id)
                        .SetProperty(
                            token =>
                                token.ConcurrencyToken,
                            Guid.NewGuid()));

        if (consumedCount != 1)
        {
            await transaction.RollbackAsync();

            _context.Entry(replacement).State =
                EntityState.Detached;

            return false;
        }

        await _context.AuthenticationSessions
            .Where(session =>
                session.Id ==
                    replacement
                        .AuthenticationSessionId)
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(
                        session =>
                            session.LastActivityAtUtc,
                        consumedAtUtc)
                    .SetProperty(
                        session =>
                            session.ConcurrencyToken,
                        Guid.NewGuid()));

        await transaction.CommitAsync();

        return true;
    }

    public async Task<bool> ReplaceSession(
        Guid currentSessionId,
        Guid userId,
        AuthenticationSession replacementSession,
        AuthenticationRefreshToken replacementToken,
        DateTime replacedAtUtc,
        string reason)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        var targetIsEligible =
            await _context
                .OrganizationMemberships
                .IgnoreQueryFilters()
                .AnyAsync(membership =>
                    membership.Id ==
                        replacementSession
                            .OrganizationMembershipId &&
                    membership.UserId == userId &&
                    membership.OrganizationId ==
                        replacementSession
                            .OrganizationId &&
                    membership.IsActive &&
                    membership.Organization.IsActive &&
                    membership.User.IsActive &&
                    membership.User
                        .EmailVerifiedAtUtc.HasValue &&
                    membership.User.SecurityStamp ==
                        replacementSession
                            .SecurityStamp);

        if (!targetIsEligible)
        {
            await transaction.RollbackAsync();
            return false;
        }

        var revokedCount =
            await _context.AuthenticationSessions
                .Where(session =>
                    session.Id == currentSessionId &&
                    session.UserId == userId &&
                    session.RevokedAtUtc == null &&
                    session.ExpiresAtUtc >
                        replacedAtUtc &&
                    session.SecurityStamp ==
                        replacementSession
                            .SecurityStamp)
                .ExecuteUpdateAsync(setters =>
                    setters
                        .SetProperty(
                            session =>
                                session.RevokedAtUtc,
                            replacedAtUtc)
                        .SetProperty(
                            session =>
                                session.RevocationReason,
                            reason)
                        .SetProperty(
                            session =>
                                session
                                    .ConcurrencyToken,
                            Guid.NewGuid()));

        if (revokedCount != 1)
        {
            await transaction.RollbackAsync();
            return false;
        }

        await _context.AuthenticationRefreshTokens
            .Where(token =>
                token.AuthenticationSessionId ==
                    currentSessionId &&
                token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(
                        token =>
                            token.RevokedAtUtc,
                        replacedAtUtc)
                    .SetProperty(
                        token =>
                            token.ConcurrencyToken,
                        Guid.NewGuid()));

        await _context.AuthenticationSessions
            .AddAsync(replacementSession);

        await _context.AuthenticationRefreshTokens
            .AddAsync(replacementToken);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return true;
    }

    public Task<bool> IsSessionActive(
        Guid sessionId,
        Guid userId,
        Guid organizationMembershipId,
        DateTime nowUtc)
    {
        return _context.AuthenticationSessions
            .AsNoTracking()
            .AnyAsync(session =>
                session.Id == sessionId &&
                session.UserId == userId &&
                session.OrganizationMembershipId ==
                    organizationMembershipId &&
                session.SecurityStamp ==
                    session.User.SecurityStamp &&
                session.RevokedAtUtc == null &&
                session.ExpiresAtUtc > nowUtc);
    }

    public Task RevokeSession(
        Guid sessionId,
        DateTime revokedAtUtc,
        string reason)
    {
        return RevokeSessions(
            session =>
                session.Id == sessionId,
            revokedAtUtc,
            reason);
    }

    public Task RevokeSessionsForMembership(
        Guid organizationMembershipId,
        DateTime revokedAtUtc,
        string reason)
    {
        return RevokeSessions(
            session =>
                session.OrganizationMembershipId ==
                    organizationMembershipId,
            revokedAtUtc,
            reason);
    }

    public Task RevokeSessionsForUser(
        Guid userId,
        DateTime revokedAtUtc,
        string reason)
    {
        return RevokeSessions(
            session =>
                session.UserId == userId,
            revokedAtUtc,
            reason);
    }

    public async Task<IReadOnlyList<
        AuthenticationSession>>
        GetActiveSessionsForUser(
            Guid userId,
            DateTime nowUtc)
    {
        return await _context
            .AuthenticationSessions
            .AsNoTracking()
            .Include(session =>
                session.Organization)
            .Where(session =>
                session.UserId == userId &&
                session.RevokedAtUtc == null &&
                session.ExpiresAtUtc > nowUtc)
            .OrderByDescending(session =>
                session.LastActivityAtUtc)
            .ToListAsync();
    }

    public async Task<bool> RevokeOwnedSession(
        Guid sessionId,
        Guid userId,
        DateTime revokedAtUtc,
        string reason)
    {
        var count = await RevokeSessionsAndCount(
            session =>
                session.Id == sessionId &&
                session.UserId == userId,
            revokedAtUtc,
            reason);

        return count == 1;
    }

    public Task<int> RevokeOtherSessions(
        Guid userId,
        Guid currentSessionId,
        DateTime revokedAtUtc,
        string reason)
    {
        return RevokeSessionsAndCount(
            session =>
                session.UserId == userId &&
                session.Id != currentSessionId,
            revokedAtUtc,
            reason);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }

    private async Task RevokeSessions(
        System.Linq.Expressions.Expression<
            Func<AuthenticationSession, bool>>
            predicate,
        DateTime revokedAtUtc,
        string reason)
    {
        _ = await RevokeSessionsAndCount(
            predicate,
            revokedAtUtc,
            reason);
    }

    private async Task<int> RevokeSessionsAndCount(
        System.Linq.Expressions.Expression<
            Func<AuthenticationSession, bool>>
            predicate,
        DateTime revokedAtUtc,
        string reason)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        var sessionIds =
            _context.AuthenticationSessions
                .Where(predicate)
                .Select(session => session.Id);

        var revokedSessionCount =
            await _context.AuthenticationSessions
            .Where(predicate)
            .Where(session =>
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
                    token.AuthenticationSessionId))
            .Where(token =>
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

        await transaction.CommitAsync();

        return revokedSessionCount;
    }
}
