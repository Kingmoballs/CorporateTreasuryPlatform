using Microsoft.EntityFrameworkCore;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Repositories;

namespace Treasury.Tests.Integration;

public class PasswordRecoveryIntegrationTests
{
    [Fact]
    public async Task
        ConcurrentRequestsAndConsumptionHaveSingleWinner()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var now =
            new DateTime(
                2026,
                7,
                23,
                19,
                30,
                0,
                DateTimeKind.Utc);

        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var refreshTokenId = Guid.NewGuid();
        var originalSecurityStamp =
            Guid.NewGuid();

        await using (var seedContext =
            database.CreateSystemContext())
        {
            var organization =
                await seedContext.Organizations
                    .OrderBy(item =>
                        item.CreatedAtUtc)
                    .FirstAsync();

            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name =
                    "Password Recovery Test " +
                    Guid.NewGuid().ToString("N")
            };

            var user = new User
            {
                Id = userId,
                FirstName = "Recovery",
                LastName = "Tester",
                Email =
                    $"{Guid.NewGuid():N}@example.com",
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        "OriginalPassword123!"),
                EmailVerifiedAtUtc =
                    now.AddDays(-10),
                SecurityStamp =
                    originalSecurityStamp,
                RoleId = role.Id,
                Role = role,
                IsActive = true
            };

            var membership =
                new OrganizationMembership
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        organization.Id,
                    Organization = organization,
                    UserId = user.Id,
                    User = user,
                    RoleId = role.Id,
                    Role = role,
                    IsActive = true,
                    IsDefault = true,
                    JoinedAtUtc =
                        now.AddDays(-10)
                };

            user.OrganizationMemberships.Add(
                membership);

            var session =
                new AuthenticationSession
                {
                    Id = sessionId,
                    UserId = user.Id,
                    User = user,
                    OrganizationId =
                        organization.Id,
                    Organization = organization,
                    OrganizationMembershipId =
                        membership.Id,
                    OrganizationMembership =
                        membership,
                    CreatedAtUtc =
                        now.AddHours(-1),
                    LastActivityAtUtc =
                        now.AddHours(-1),
                    ExpiresAtUtc =
                        now.AddDays(7),
                    SecurityStamp =
                        originalSecurityStamp
                };

            var refreshToken =
                new AuthenticationRefreshToken
                {
                    Id = refreshTokenId,
                    AuthenticationSessionId =
                        session.Id,
                    AuthenticationSession =
                        session,
                    TokenHash =
                        new string('R', 64),
                    CreatedAtUtc =
                        now.AddHours(-1),
                    ExpiresAtUtc =
                        now.AddDays(7)
                };

            session.RefreshTokens.Add(
                refreshToken);

            await seedContext
                .AuthenticationSessions
                .AddAsync(session);

            await seedContext.SaveChangesAsync();
        }

        var firstResetToken =
            CreateToken(
                userId,
                'A',
                now);

        var secondResetToken =
            CreateToken(
                userId,
                'B',
                now);

        bool[] requestResults;

        await using (var firstContext =
            database.CreateSystemContext())
        await using (var secondContext =
            database.CreateSystemContext())
        {
            var firstRepository =
                new PasswordResetTokenRepository(
                    firstContext);

            var secondRepository =
                new PasswordResetTokenRepository(
                    secondContext);

            requestResults =
                await Task.WhenAll(
                    firstRepository.TryCreate(
                        firstResetToken,
                        now.AddMinutes(-5)),
                    secondRepository.TryCreate(
                        secondResetToken,
                        now.AddMinutes(-5)));
        }

        Assert.Single(
            requestResults,
            result => result);

        Assert.Single(
            requestResults,
            result => !result);

        Guid winningTokenId;

        await using (var lookupContext =
            database.CreateSystemContext())
        {
            winningTokenId =
                await lookupContext
                    .PasswordResetTokens
                    .Where(token =>
                        token.UserId == userId &&
                        token.ConsumedAtUtc == null &&
                        token.RevokedAtUtc == null)
                    .Select(token => token.Id)
                    .SingleAsync();
        }

        var changedAt =
            now.AddMinutes(1);

        var firstPasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                "FirstNewPassword123!");

        var secondPasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                "SecondNewPassword123!");

        var firstSecurityStamp =
            Guid.NewGuid();

        var secondSecurityStamp =
            Guid.NewGuid();

        bool[] consumeResults;

        await using (var firstContext =
            database.CreateSystemContext())
        await using (var secondContext =
            database.CreateSystemContext())
        {
            var firstRepository =
                new PasswordResetTokenRepository(
                    firstContext);

            var secondRepository =
                new PasswordResetTokenRepository(
                    secondContext);

            consumeResults =
                await Task.WhenAll(
                    firstRepository
                        .ConsumeAndChangePassword(
                            winningTokenId,
                            userId,
                            firstPasswordHash,
                            firstSecurityStamp,
                            changedAt),
                    secondRepository
                        .ConsumeAndChangePassword(
                            winningTokenId,
                            userId,
                            secondPasswordHash,
                            secondSecurityStamp,
                            changedAt));
        }

        Assert.Single(
            consumeResults,
            result => result);

        Assert.Single(
            consumeResults,
            result => !result);

        await using var verificationContext =
            database.CreateSystemContext();

        var storedUser =
            await verificationContext.Users
                .AsNoTracking()
                .SingleAsync(user =>
                    user.Id == userId);

        var winningPasswordHash =
            consumeResults[0]
                ? firstPasswordHash
                : secondPasswordHash;

        var winningSecurityStamp =
            consumeResults[0]
                ? firstSecurityStamp
                : secondSecurityStamp;

        Assert.Equal(
            winningPasswordHash,
            storedUser.PasswordHash);
        Assert.Equal(
            winningSecurityStamp,
            storedUser.SecurityStamp);
        Assert.Equal(
            changedAt,
            storedUser.PasswordChangedAtUtc);

        var storedResetToken =
            await verificationContext
                .PasswordResetTokens
                .AsNoTracking()
                .SingleAsync(token =>
                    token.Id == winningTokenId);

        Assert.Equal(
            changedAt,
            storedResetToken.ConsumedAtUtc);

        var storedSession =
            await verificationContext
                .AuthenticationSessions
                .AsNoTracking()
                .SingleAsync(session =>
                    session.Id == sessionId);

        Assert.Equal(
            changedAt,
            storedSession.RevokedAtUtc);
        Assert.Equal(
            "Password changed.",
            storedSession.RevocationReason);

        var storedRefreshToken =
            await verificationContext
                .AuthenticationRefreshTokens
                .AsNoTracking()
                .SingleAsync(token =>
                    token.Id == refreshTokenId);

        Assert.Equal(
            changedAt,
            storedRefreshToken.RevokedAtUtc);
    }

    private static PasswordResetToken CreateToken(
        Guid userId,
        char hashCharacter,
        DateTime now)
    {
        return new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash =
                new string(hashCharacter, 64),
            CreatedAtUtc = now,
            ExpiresAtUtc =
                now.AddMinutes(30)
        };
    }
}
