using Microsoft.EntityFrameworkCore;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Repositories;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public class OrganizationSwitchIntegrationTests
{
    [Fact]
    public async Task
        SwitchAtomicallyReplacesOnlyOwnedSession()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var now =
            new DateTime(
                2026,
                7,
                24,
                9,
                0,
                0,
                DateTimeKind.Utc);

        SeededData seeded;

        await using (var seedContext =
            database.CreateSystemContext())
        {
            seeded = await Seed(
                seedContext,
                now);
        }

        await using var context =
            database.CreateContext(
                seeded.SourceOrganizationId);

        var accessRepository =
            new OrganizationAccessRepository(
                context);

        var memberships =
            await accessRepository
                .GetActiveMembershipsForUser(
                    seeded.UserId);

        Assert.Equal(2, memberships.Count);
        Assert.DoesNotContain(
            memberships,
            item =>
                item.Id ==
                    seeded.OtherUserMembershipId);

        var target =
            await accessRepository
                .GetActiveMembershipForUser(
                    seeded.TargetMembershipId,
                    seeded.UserId);

        Assert.NotNull(target);

        var sessionRepository =
            new AuthenticationSessionRepository(
                context);

        var replacement =
            CreateReplacementSession(
                seeded,
                now);

        var replacementToken =
            CreateReplacementToken(
                replacement,
                now,
                "C");

        var switched =
            await sessionRepository.ReplaceSession(
                seeded.SourceSessionId,
                seeded.UserId,
                replacement,
                replacementToken,
                now,
                "Organization switch.");

        Assert.True(switched);

        var sourceSession = await context
            .AuthenticationSessions
            .AsNoTracking()
            .SingleAsync(item =>
                item.Id ==
                    seeded.SourceSessionId);

        var sourceToken = await context
            .AuthenticationRefreshTokens
            .AsNoTracking()
            .SingleAsync(item =>
                item.AuthenticationSessionId ==
                    seeded.SourceSessionId);

        var persistedReplacement = await context
            .AuthenticationSessions
            .AsNoTracking()
            .SingleAsync(item =>
                item.Id == replacement.Id);

        Assert.Equal(
            now,
            sourceSession.RevokedAtUtc);
        Assert.Equal(now, sourceToken.RevokedAtUtc);
        Assert.Null(
            persistedReplacement.RevokedAtUtc);
        Assert.Equal(
            seeded.TargetOrganizationId,
            persistedReplacement.OrganizationId);
        Assert.Equal(
            seeded.TargetMembershipId,
            persistedReplacement
                .OrganizationMembershipId);

        var concurrentReplacement =
            CreateReplacementSession(
                seeded,
                now.AddSeconds(1));

        var concurrentToken =
            CreateReplacementToken(
                concurrentReplacement,
                now.AddSeconds(1),
                "D");

        var concurrentSwitch =
            await sessionRepository.ReplaceSession(
                seeded.SourceSessionId,
                seeded.UserId,
                concurrentReplacement,
                concurrentToken,
                now.AddSeconds(1),
                "Concurrent organization switch.");

        Assert.False(concurrentSwitch);
        Assert.False(
            await context.AuthenticationSessions
                .AsNoTracking()
                .AnyAsync(item =>
                    item.Id ==
                        concurrentReplacement.Id));

        var crossUserReplacement =
            CreateReplacementSession(
                seeded,
                now.AddSeconds(2));

        crossUserReplacement
            .OrganizationMembershipId =
            seeded.OtherUserMembershipId;

        var crossUserToken =
            CreateReplacementToken(
                crossUserReplacement,
                now.AddSeconds(2),
                "E");

        var crossUserSwitch =
            await sessionRepository.ReplaceSession(
                replacement.Id,
                seeded.UserId,
                crossUserReplacement,
                crossUserToken,
                now.AddSeconds(2),
                "Cross-user organization switch.");

        Assert.False(crossUserSwitch);

        var stillActive = await context
            .AuthenticationSessions
            .AsNoTracking()
            .SingleAsync(item =>
                item.Id == replacement.Id);

        Assert.Null(stillActive.RevokedAtUtc);
    }

    private static async Task<SeededData> Seed(
        Treasury.Infrastructure.Persistence
            .TreasuryDbContext context,
        DateTime now)
    {
        var sourceOrganization =
            await context.Organizations
                .OrderBy(item =>
                    item.CreatedAtUtc)
                .FirstAsync();

        var targetOrganization =
            CreateOrganization("TARGET", now);

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name =
                "Organization Switch " +
                Guid.NewGuid().ToString("N")
        };

        var user = CreateUser(role, now);
        var otherUser = CreateUser(role, now);

        var sourceMembership =
            CreateMembership(
                sourceOrganization,
                user,
                role,
                now,
                isDefault: true);

        var targetMembership =
            CreateMembership(
                targetOrganization,
                user,
                role,
                now,
                isDefault: false);

        var otherUserMembership =
            CreateMembership(
                targetOrganization,
                otherUser,
                role,
                now,
                isDefault: true);

        user.OrganizationMemberships.Add(
            sourceMembership);
        user.OrganizationMemberships.Add(
            targetMembership);
        otherUser.OrganizationMemberships.Add(
            otherUserMembership);

        var sourceSession =
            new AuthenticationSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                User = user,
                OrganizationId =
                    sourceOrganization.Id,
                Organization =
                    sourceOrganization,
                OrganizationMembershipId =
                    sourceMembership.Id,
                OrganizationMembership =
                    sourceMembership,
                CreatedAtUtc = now.AddHours(-1),
                LastActivityAtUtc =
                    now.AddMinutes(-5),
                ExpiresAtUtc = now.AddDays(7),
                AuthenticationMethod =
                    AuthenticationMethods.Password,
                SecurityStamp =
                    user.SecurityStamp
            };

        var sourceToken =
            CreateReplacementToken(
                sourceSession,
                now.AddHours(-1),
                "B");

        await context.Organizations.AddAsync(
            targetOrganization);

        await context.Users.AddRangeAsync(
            user,
            otherUser);

        await context.AuthenticationSessions
            .AddAsync(sourceSession);

        await context.AuthenticationRefreshTokens
            .AddAsync(sourceToken);

        await context.SaveChangesAsync();

        return new SeededData(
            user.Id,
            user.SecurityStamp,
            sourceOrganization.Id,
            targetOrganization.Id,
            sourceMembership.Id,
            targetMembership.Id,
            otherUserMembership.Id,
            sourceSession.Id);
    }

    private static Organization
        CreateOrganization(
            string prefix,
            DateTime now)
    {
        var suffix =
            Guid.NewGuid()
                .ToString("N")[..8];

        return new Organization
        {
            Id = Guid.NewGuid(),
            Code =
                $"{prefix}-{suffix}"
                    .ToUpperInvariant(),
            Name =
                $"{prefix} Organization {suffix}",
            Slug =
                $"{prefix.ToLowerInvariant()}-" +
                suffix,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static User CreateUser(
        Role role,
        DateTime now)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Organization",
            LastName = "Switcher",
            Email =
                $"{Guid.NewGuid():N}@example.com",
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
            DateTime now,
            bool isDefault)
    {
        return new OrganizationMembership
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Organization = organization,
            UserId = user.Id,
            User = user,
            RoleId = role.Id,
            Role = role,
            IsActive = true,
            IsDefault = isDefault,
            JoinedAtUtc = now
        };
    }

    private static AuthenticationSession
        CreateReplacementSession(
            SeededData seeded,
            DateTime now)
    {
        return new AuthenticationSession
        {
            Id = Guid.NewGuid(),
            UserId = seeded.UserId,
            OrganizationId =
                seeded.TargetOrganizationId,
            OrganizationMembershipId =
                seeded.TargetMembershipId,
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            ExpiresAtUtc = now.AddDays(7),
            AuthenticationMethod =
                AuthenticationMethods
                    .OrganizationSwitch,
            SecurityStamp = seeded.SecurityStamp
        };
    }

    private static AuthenticationRefreshToken
        CreateReplacementToken(
            AuthenticationSession session,
            DateTime now,
            string suffix)
    {
        return new AuthenticationRefreshToken
        {
            Id = Guid.NewGuid(),
            AuthenticationSessionId =
                session.Id,
            AuthenticationSession = session,
            TokenHash =
                suffix.PadLeft(64, '0'),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(7)
        };
    }

    private sealed record SeededData(
        Guid UserId,
        Guid SecurityStamp,
        Guid SourceOrganizationId,
        Guid TargetOrganizationId,
        Guid SourceMembershipId,
        Guid TargetMembershipId,
        Guid OtherUserMembershipId,
        Guid SourceSessionId);
}
