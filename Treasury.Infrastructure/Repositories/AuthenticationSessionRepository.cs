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
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        var sessionIds =
            _context.AuthenticationSessions
                .Where(predicate)
                .Select(session => session.Id);

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
    }
}
