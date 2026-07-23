using Microsoft.EntityFrameworkCore;
using Moq;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public class
    AuthenticationSecurityEventIntegrationTests
{
    [Fact]
    public async Task
        EventsAndSessionsEnforceSecurityBoundaries()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var now =
            new DateTime(
                2026,
                7,
                23,
                23,
                0,
                0,
                DateTimeKind.Utc);

        SeededData seeded;

        await using (var context =
            database.CreateSystemContext())
        {
            seeded = await Seed(
                context,
                now);
        }

        await VerifyTenantScopedSearch(
            database,
            seeded);

        await VerifyEventsAreImmutable(
            database,
            seeded);

        await VerifySessionOwnership(
            database,
            seeded,
            now);

        await VerifyRetentionRequiresSystemScope(
            database,
            seeded,
            now);
    }

    private static async Task
        VerifyTenantScopedSearch(
            PostgreSqlTestDatabase database,
            SeededData seeded)
    {
        await using var context =
            database.CreateContext(
                seeded.OrganizationOneId);

        var repository =
            new AuthenticationSecurityEventRepository(
                context);

        var currentUser =
            new Mock<ICurrentUserService>();

        currentUser
            .SetupGet(item => item.OrganizationId)
            .Returns(seeded.OrganizationOneId);

        var service =
            new AuthenticationSecurityEventService(
                repository,
                currentUser.Object,
                Mock.Of<IClientRequestContext>(),
                TimeProvider.System);

        var result = await service.Search(
            new AuthenticationSecurityEventQueryDto
            {
                Page = 1,
                PageSize = 50
            });

        Assert.Single(result.Items);
        Assert.Equal(
            seeded.OrganizationOneEventId,
            result.Items[0].Id);
    }

    private static async Task
        VerifyEventsAreImmutable(
            PostgreSqlTestDatabase database,
            SeededData seeded)
    {
        await using var context =
            database.CreateSystemContext();

        var item = await context
            .AuthenticationSecurityEvents
            .SingleAsync(value =>
                value.Id ==
                    seeded.OrganizationTwoEventId);

        item.ReasonCode = "tampered";

        var exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () => context.SaveChangesAsync());

        Assert.Contains(
            "immutable",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task
        VerifySessionOwnership(
            PostgreSqlTestDatabase database,
            SeededData seeded,
            DateTime now)
    {
        await using var context =
            database.CreateContext(
                seeded.OrganizationOneId);

        var repository =
            new AuthenticationSessionRepository(
                context);

        var crossUserRevoked =
            await repository.RevokeOwnedSession(
                seeded.UserTwoSessionId,
                seeded.UserOneId,
                now,
                "Cross-user attempt.");

        Assert.False(crossUserRevoked);

        var ownSessionRevoked =
            await repository.RevokeOwnedSession(
                seeded.UserOneSessionId,
                seeded.UserOneId,
                now,
                "User requested revocation.");

        Assert.True(ownSessionRevoked);

        var userOneSession = await context
            .AuthenticationSessions
            .AsNoTracking()
            .SingleAsync(item =>
                item.Id ==
                    seeded.UserOneSessionId);

        var userTwoSession = await context
            .AuthenticationSessions
            .AsNoTracking()
            .SingleAsync(item =>
                item.Id ==
                    seeded.UserTwoSessionId);

        var userOneToken = await context
            .AuthenticationRefreshTokens
            .AsNoTracking()
            .SingleAsync(item =>
                item.AuthenticationSessionId ==
                    seeded.UserOneSessionId);

        Assert.Equal(
            now,
            userOneSession.RevokedAtUtc);
        Assert.Equal(
            now,
            userOneToken.RevokedAtUtc);
        Assert.Null(userTwoSession.RevokedAtUtc);
    }

    private static async Task
        VerifyRetentionRequiresSystemScope(
            PostgreSqlTestDatabase database,
            SeededData seeded,
            DateTime now)
    {
        await using (var tenantContext =
            database.CreateContext(
                seeded.OrganizationOneId))
        {
            var tenantRepository =
                new
                    AuthenticationSecurityEventRepository(
                        tenantContext);

            await Assert.ThrowsAsync<
                UnauthorizedAccessException>(
                () => tenantRepository
                    .DeleteOlderThan(
                        now.AddDays(-30),
                        100));
        }

        await using var systemContext =
            database.CreateSystemContext();

        var systemRepository =
            new AuthenticationSecurityEventRepository(
                systemContext);

        var deleted =
            await systemRepository.DeleteOlderThan(
                now.AddDays(-30),
                100);

        Assert.Equal(1, deleted);

        var remainingIds = await systemContext
            .AuthenticationSecurityEvents
            .AsNoTracking()
            .Select(item => item.Id)
            .ToListAsync();

        Assert.DoesNotContain(
            seeded.OrganizationOneEventId,
            remainingIds);
        Assert.Contains(
            seeded.OrganizationTwoEventId,
            remainingIds);
    }

    private static async Task<SeededData> Seed(
        Treasury.Infrastructure.Persistence
            .TreasuryDbContext context,
        DateTime now)
    {
        var organizationOne =
            await context.Organizations
                .OrderBy(item =>
                    item.CreatedAtUtc)
                .FirstAsync();

        var organizationTwo =
            new Organization
            {
                Id = Guid.NewGuid(),
                Code =
                    "AUTH-" +
                    Guid.NewGuid()
                        .ToString("N")[..8]
                        .ToUpperInvariant(),
                Name = "Authentication Security Test",
                Slug =
                    "authentication-security-" +
                    Guid.NewGuid()
                        .ToString("N")[..8],
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name =
                "Authentication Security Test " +
                Guid.NewGuid().ToString("N")
        };

        var userOne = CreateUser(
            role,
            "one",
            now);

        var userTwo = CreateUser(
            role,
            "two",
            now);

        var membershipOne = CreateMembership(
            organizationOne,
            userOne,
            role,
            now);

        var membershipTwo = CreateMembership(
            organizationOne,
            userTwo,
            role,
            now);

        userOne.OrganizationMemberships.Add(
            membershipOne);

        userTwo.OrganizationMemberships.Add(
            membershipTwo);

        var sessionOne = CreateSession(
            organizationOne,
            userOne,
            membershipOne,
            now);

        var sessionTwo = CreateSession(
            organizationOne,
            userTwo,
            membershipTwo,
            now);

        var tokenOne = CreateToken(
            sessionOne,
            "A",
            now);

        var tokenTwo = CreateToken(
            sessionTwo,
            "B",
            now);

        var oldEvent =
            new AuthenticationSecurityEvent
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    organizationOne.Id,
                UserId = userOne.Id,
                AuthenticationSessionId =
                    sessionOne.Id,
                EventType =
                    AuthenticationSecurityEventTypes
                        .LoginSucceeded,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Succeeded,
                OccurredAtUtc =
                    now.AddDays(-120)
            };

        var currentEvent =
            new AuthenticationSecurityEvent
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    organizationTwo.Id,
                EventType =
                    AuthenticationSecurityEventTypes
                        .LoginFailed,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Failed,
                OccurredAtUtc = now
            };

        await context.Organizations.AddAsync(
            organizationTwo);

        await context.Users.AddRangeAsync(
            userOne,
            userTwo);

        await context.AuthenticationSessions
            .AddRangeAsync(
                sessionOne,
                sessionTwo);

        await context.AuthenticationRefreshTokens
            .AddRangeAsync(
                tokenOne,
                tokenTwo);

        await context.AuthenticationSecurityEvents
            .AddRangeAsync(
                oldEvent,
                currentEvent);

        await context.SaveChangesAsync();

        return new SeededData(
            organizationOne.Id,
            oldEvent.Id,
            currentEvent.Id,
            userOne.Id,
            sessionOne.Id,
            sessionTwo.Id);
    }

    private static User CreateUser(
        Role role,
        string suffix,
        DateTime now)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Security",
            LastName = "Tester",
            Email =
                $"{Guid.NewGuid():N}-{suffix}" +
                "@example.com",
            PasswordHash = "not-used",
            EmailVerifiedAtUtc = now,
            RoleId = role.Id,
            Role = role,
            CreatedAt = now
        };
    }

    private static OrganizationMembership
        CreateMembership(
            Organization organization,
            User user,
            Role role,
            DateTime now)
    {
        return new OrganizationMembership
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
    }

    private static AuthenticationSession
        CreateSession(
            Organization organization,
            User user,
            OrganizationMembership membership,
            DateTime now)
    {
        return new AuthenticationSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            OrganizationId = organization.Id,
            Organization = organization,
            OrganizationMembershipId =
                membership.Id,
            OrganizationMembership =
                membership,
            CreatedAtUtc = now.AddHours(-1),
            LastActivityAtUtc = now,
            ExpiresAtUtc = now.AddDays(7),
            AuthenticationMethod =
                AuthenticationMethods.Password,
            SecurityStamp = user.SecurityStamp
        };
    }

    private static AuthenticationRefreshToken
        CreateToken(
            AuthenticationSession session,
            string suffix,
            DateTime now)
    {
        return new AuthenticationRefreshToken
        {
            Id = Guid.NewGuid(),
            AuthenticationSessionId =
                session.Id,
            AuthenticationSession = session,
            TokenHash =
                suffix.PadLeft(64, '0'),
            CreatedAtUtc = now.AddHours(-1),
            ExpiresAtUtc = now.AddDays(7)
        };
    }

    private sealed record SeededData(
        Guid OrganizationOneId,
        Guid OrganizationOneEventId,
        Guid OrganizationTwoEventId,
        Guid UserOneId,
        Guid UserOneSessionId,
        Guid UserTwoSessionId);
}
