using Microsoft.EntityFrameworkCore;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Repositories;

namespace Treasury.Tests.Integration;

public class AuthenticationSessionIntegrationTests
{
    [Fact]
    public async Task
        ConcurrentRefreshRotation_OnlyOneReplacementIsCommitted()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var now = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();
        var currentTokenId = Guid.NewGuid();

        await using (var seedContext =
            database.CreateSystemContext())
        {
            var organization =
                await seedContext.Organizations
                    .OrderBy(item => item.CreatedAtUtc)
                    .FirstAsync();

            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name =
                    "Session Test " +
                    Guid.NewGuid().ToString("N")
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Session",
                LastName = "Tester",
                Email =
                    $"{Guid.NewGuid():N}@example.com",
                PasswordHash = "not-used",
                EmailVerifiedAtUtc = now,
                RoleId = role.Id,
                Role = role
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
                    JoinedAtUtc = now
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
                    CreatedAtUtc = now,
                    LastActivityAtUtc = now,
                    ExpiresAtUtc =
                        now.AddDays(7)
                };

            var refreshToken =
                new AuthenticationRefreshToken
                {
                    Id = currentTokenId,
                    AuthenticationSessionId =
                        session.Id,
                    AuthenticationSession =
                        session,
                    TokenHash =
                        new string('A', 64),
                    CreatedAtUtc = now,
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

        await using var firstContext =
            database.CreateSystemContext();

        await using var secondContext =
            database.CreateSystemContext();

        var firstRepository =
            new AuthenticationSessionRepository(
                firstContext);

        var secondRepository =
            new AuthenticationSessionRepository(
                secondContext);

        var firstReplacement =
            CreateReplacement(
                sessionId,
                'B',
                now);

        var secondReplacement =
            CreateReplacement(
                sessionId,
                'C',
                now);

        var results =
            await Task.WhenAll(
                firstRepository.RotateRefreshToken(
                    currentTokenId,
                    firstReplacement,
                    now.AddMinutes(1)),
                secondRepository.RotateRefreshToken(
                    currentTokenId,
                    secondReplacement,
                    now.AddMinutes(1)));

        Assert.Single(
            results,
            result => result);

        Assert.Single(
            results,
            result => !result);

        await using var verificationContext =
            database.CreateSystemContext();

        var storedTokens =
            await verificationContext
                .AuthenticationRefreshTokens
                .Where(token =>
                    token.AuthenticationSessionId ==
                        sessionId)
                .OrderBy(token =>
                    token.CreatedAtUtc)
                .ToListAsync();

        Assert.Equal(2, storedTokens.Count);

        var consumedToken =
            storedTokens.Single(token =>
                token.Id == currentTokenId);

        Assert.NotNull(
            consumedToken.ConsumedAtUtc);

        Assert.NotNull(
            consumedToken.ReplacedByTokenId);

        Assert.Contains(
            storedTokens,
            token =>
                token.Id ==
                    consumedToken
                        .ReplacedByTokenId);
    }

    private static AuthenticationRefreshToken
        CreateReplacement(
            Guid sessionId,
            char hashCharacter,
            DateTime now)
    {
        return new AuthenticationRefreshToken
        {
            Id = Guid.NewGuid(),
            AuthenticationSessionId = sessionId,
            TokenHash =
                new string(hashCharacter, 64),
            CreatedAtUtc =
                now.AddMinutes(1),
            ExpiresAtUtc =
                now.AddDays(7)
        };
    }
}
