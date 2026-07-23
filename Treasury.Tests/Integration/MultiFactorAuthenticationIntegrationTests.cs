using Microsoft.EntityFrameworkCore;
using Moq;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Repositories;

namespace Treasury.Tests.Integration;

public class MultiFactorAuthenticationIntegrationTests
{
    [Fact]
    public async Task
        MfaStateAndCredentialsAreAtomicAcrossInstances()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var now =
            new DateTime(
                2026,
                7,
                23,
                21,
                30,
                0,
                DateTimeKind.Utc);

        var userId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var refreshTokenId = Guid.NewGuid();
        var originalStamp = Guid.NewGuid();
        Guid organizationId;

        await using (var seedContext =
            database.CreateSystemContext())
        {
            var organization =
                await seedContext.Organizations
                    .OrderBy(item =>
                        item.CreatedAtUtc)
                    .FirstAsync();

            organizationId = organization.Id;

            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name =
                    "MFA Test " +
                    Guid.NewGuid().ToString("N")
            };

            var user = new User
            {
                Id = userId,
                FirstName = "MFA",
                LastName = "Tester",
                Email =
                    $"{Guid.NewGuid():N}@example.com",
                PasswordHash = "not-used",
                EmailVerifiedAtUtc =
                    now.AddDays(-10),
                ProtectedTotpSecret =
                    "protected-secret",
                MfaEnrollmentStartedAtUtc =
                    now.AddMinutes(-1),
                SecurityStamp = originalStamp,
                RoleId = role.Id,
                Role = role,
                IsActive = true
            };

            var membership =
                new OrganizationMembership
                {
                    Id = membershipId,
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
                    OrganizationId =
                        organization.Id,
                    OrganizationMembershipId =
                        membership.Id,
                    CreatedAtUtc =
                        now.AddHours(-1),
                    LastActivityAtUtc =
                        now.AddHours(-1),
                    ExpiresAtUtc =
                        now.AddDays(7),
                    SecurityStamp =
                        originalStamp
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
                        new string('F', 64),
                    CreatedAtUtc =
                        now.AddHours(-1),
                    ExpiresAtUtc =
                        now.AddDays(7)
                };

            session.RefreshTokens.Add(
                refreshToken);

            await seedContext.Users.AddAsync(user);

            await seedContext
                .AuthenticationSessions
                .AddAsync(session);

            await seedContext.SaveChangesAsync();
        }

        var enabledStamp = Guid.NewGuid();
        var recoveryCodeHash =
            new string('R', 64);

        await using (var enableContext =
            database.CreateSystemContext())
        {
            var repository =
                new MultiFactorRepository(
                    enableContext);

            var enabled =
                await repository.Enable(
                    userId,
                    originalStamp,
                    now,
                    enabledStamp,
                    new[]
                    {
                        new MfaRecoveryCode
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            CodeHash =
                                recoveryCodeHash,
                            CreatedAtUtc = now
                        }
                    });

            Assert.True(enabled);
        }

        await using (var enabledContext =
            database.CreateSystemContext())
        {
            var enabledUser =
                await enabledContext.Users
                    .AsNoTracking()
                    .SingleAsync(user =>
                        user.Id == userId);

            Assert.Equal(
                now,
                enabledUser.MfaEnabledAtUtc);
            Assert.Null(
                enabledUser
                    .MfaEnrollmentStartedAtUtc);
            Assert.Equal(
                enabledStamp,
                enabledUser.SecurityStamp);

            var revokedSession =
                await enabledContext
                    .AuthenticationSessions
                    .AsNoTracking()
                    .SingleAsync(session =>
                        session.Id == sessionId);

            Assert.Equal(
                now,
                revokedSession.RevokedAtUtc);

            var revokedRefreshToken =
                await enabledContext
                    .AuthenticationRefreshTokens
                    .AsNoTracking()
                    .SingleAsync(token =>
                        token.Id == refreshTokenId);

            Assert.Equal(
                now,
                revokedRefreshToken.RevokedAtUtc);
        }

        await using (var staleRequestContext =
            database.CreateSystemContext())
        {
            var staleMutationAccepted =
                await new MultiFactorRepository(
                        staleRequestContext)
                    .Disable(
                        userId,
                        originalStamp,
                        now.AddSeconds(1),
                        Guid.NewGuid());

            Assert.False(staleMutationAccepted);
        }

        var lockedChallenge =
            CreateChallenge(
                userId,
                organizationId,
                membershipId,
                enabledStamp,
                'A',
                now.AddMinutes(1));

        await CreateChallenge(
            database,
            lockedChallenge);

        var attemptContexts =
            Enumerable.Range(0, 5)
                .Select(_ =>
                    database.CreateSystemContext())
                .ToList();

        try
        {
            await Task.WhenAll(
                attemptContexts.Select(context =>
                    new MultiFactorRepository(context)
                        .RecordFailedChallengeAttempt(
                            lockedChallenge.Id,
                            now.AddMinutes(2),
                            maximumAttempts: 5)));
        }
        finally
        {
            foreach (var context in
                     attemptContexts)
            {
                await context.DisposeAsync();
            }
        }

        await using (var lockContext =
            database.CreateSystemContext())
        {
            var storedChallenge =
                await lockContext.MfaLoginChallenges
                    .AsNoTracking()
                    .SingleAsync(challenge =>
                        challenge.Id ==
                            lockedChallenge.Id);

            Assert.Equal(
                5,
                storedChallenge.FailedAttempts);
            Assert.Equal(
                now.AddMinutes(2),
                storedChallenge.LockedAtUtc);
        }

        var totpChallenge =
            CreateChallenge(
                userId,
                organizationId,
                membershipId,
                enabledStamp,
                'B',
                now.AddMinutes(3));

        await CreateChallenge(
            database,
            totpChallenge);

        var consumeResults =
            await ConsumeConcurrently(
                database,
                repository =>
                    repository.ConsumeChallenge(
                        totpChallenge.Id,
                        userId,
                        now.AddMinutes(4),
                        maximumAttempts: 5));

        Assert.Single(
            consumeResults,
            result => result);
        Assert.Single(
            consumeResults,
            result => !result);

        var recoveryChallenge =
            CreateChallenge(
                userId,
                organizationId,
                membershipId,
                enabledStamp,
                'C',
                now.AddMinutes(5));

        await CreateChallenge(
            database,
            recoveryChallenge);

        var recoveryResults =
            await ConsumeConcurrently(
                database,
                repository =>
                    repository
                        .ConsumeChallengeWithRecoveryCode(
                            recoveryChallenge.Id,
                            userId,
                            recoveryCodeHash,
                            now.AddMinutes(6),
                            maximumAttempts: 5));

        Assert.Single(
            recoveryResults,
            result => result);
        Assert.Single(
            recoveryResults,
            result => !result);

        await using var verificationContext =
            database.CreateSystemContext();

        var storedRecoveryCode =
            await verificationContext.MfaRecoveryCodes
                .AsNoTracking()
                .SingleAsync(code =>
                    code.CodeHash ==
                        recoveryCodeHash);

        Assert.Equal(
            now.AddMinutes(6),
            storedRecoveryCode.ConsumedAtUtc);

        var storedRecoveryChallenge =
            await verificationContext
                .MfaLoginChallenges
                .AsNoTracking()
                .SingleAsync(challenge =>
                    challenge.Id ==
                        recoveryChallenge.Id);

        Assert.Equal(
            now.AddMinutes(6),
            storedRecoveryChallenge.ConsumedAtUtc);
    }

    private static async Task CreateChallenge(
        PostgreSqlTestDatabase database,
        MfaLoginChallenge challenge)
    {
        await using var context =
            database.CreateSystemContext();

        var created =
            await new MultiFactorRepository(context)
                .TryCreateChallenge(challenge);

        Assert.True(created);
    }

    private static async Task<bool[]>
        ConsumeConcurrently(
            PostgreSqlTestDatabase database,
            Func<MultiFactorRepository, Task<bool>>
                consume)
    {
        await using var firstContext =
            database.CreateSystemContext();

        await using var secondContext =
            database.CreateSystemContext();

        return await Task.WhenAll(
            consume(
                new MultiFactorRepository(
                    firstContext)),
            consume(
                new MultiFactorRepository(
                    secondContext)));
    }

    private static MfaLoginChallenge
        CreateChallenge(
            Guid userId,
            Guid organizationId,
            Guid membershipId,
            Guid securityStamp,
            char hashCharacter,
            DateTime createdAtUtc)
    {
        return new MfaLoginChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            OrganizationMembershipId =
                membershipId,
            TokenHash =
                new string(hashCharacter, 64),
            SecurityStamp = securityStamp,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc =
                createdAtUtc.AddMinutes(5)
        };
    }
}
