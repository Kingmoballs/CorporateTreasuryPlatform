using Microsoft.EntityFrameworkCore;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Persistence;

public static class PlatformAdminSeeder
{
    public static async Task Seed(
        TreasuryDbContext context,
        PlatformAdminBootstrapOptions options)
    {
        var platformAdminRole =
            await context.Roles
                .SingleOrDefaultAsync(role =>
                    role.Name ==
                    Roles.PlatformAdmin)
            ?? throw new InvalidOperationException(
                "The PlatformAdmin role has not been seeded.");

        var platformOrganization =
            await context.Organizations
                .SingleOrDefaultAsync(organization =>
                    organization.Code ==
                        PlatformDefaults.OrganizationCode ||
                    organization.Slug ==
                        PlatformDefaults.OrganizationSlug);

        if (platformOrganization is not null &&
            (platformOrganization.Code !=
                 PlatformDefaults.OrganizationCode ||
             platformOrganization.Slug !=
                 PlatformDefaults.OrganizationSlug ||
             platformOrganization.Name !=
                 PlatformDefaults.OrganizationName))
        {
            throw new InvalidOperationException(
                "The reserved platform organization code " +
                "or slug is already being used.");
        }

        if (platformOrganization is null)
        {
            platformOrganization = new Organization
            {
                Id = Guid.NewGuid(),
                Code =
                    PlatformDefaults.OrganizationCode,
                Name =
                    PlatformDefaults.OrganizationName,
                Slug =
                    PlatformDefaults.OrganizationSlug,
                CountryCode =
                    PlatformDefaults.CountryCode,
                BaseCurrency =
                    PlatformDefaults.BaseCurrency,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await context.Organizations.AddAsync(
                platformOrganization);
        }
        else if (!platformOrganization.IsActive)
        {
            throw new InvalidOperationException(
                "The reserved platform organization " +
                "is inactive.");
        }

        if (context.Entry(platformOrganization).State ==
            EntityState.Added)
        {
            await context.SaveChangesAsync();
        }

        /*
         * The reserved organization is always present so
         * anonymous applications can be audited. Creating
         * the first PlatformAdmin remains an explicit,
         * one-time bootstrap operation.
         */
        if (!options.Enabled)
        {
            return;
        }

        var normalizedEmail =
            options.Email.Trim().ToLowerInvariant();

        if (context.Entry(platformOrganization).State !=
            EntityState.Added)
        {
            var existingPlatformAdmin =
                await context.OrganizationMemberships
                    .Include(membership =>
                        membership.User)
                    .FirstOrDefaultAsync(membership =>
                        membership.OrganizationId ==
                            platformOrganization.Id &&
                        membership.RoleId ==
                            platformAdminRole.Id);

            if (existingPlatformAdmin is not null)
            {
                if (!string.Equals(
                        existingPlatformAdmin.User.Email,
                        normalizedEmail,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "A bootstrap PlatformAdmin already " +
                        "exists. Disable bootstrap configuration.");
                }

                if (!existingPlatformAdmin.IsActive ||
                    !existingPlatformAdmin.IsDefault ||
                    !existingPlatformAdmin.User.IsActive ||
                    !existingPlatformAdmin.User
                        .EmailVerifiedAtUtc.HasValue ||
                    existingPlatformAdmin.User.RoleId !=
                        platformAdminRole.Id)
                {
                    throw new InvalidOperationException(
                        "The existing PlatformAdmin account " +
                        "is not in a valid state.");
                }

                await VerifyPlatformAdmin(
                    context,
                    platformOrganization.Id,
                    platformAdminRole.Id,
                    normalizedEmail);

                // Bootstrap is idempotent. It never changes
                // the password of an existing administrator.
                return;
            }
        }

        var existingUser =
            await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user =>
                    user.Email.ToLower() ==
                    normalizedEmail);

        if (existingUser is not null)
        {
            throw new InvalidOperationException(
                "The bootstrap email already belongs to " +
                "another user. Use a different email.");
        }

        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = options.FirstName.Trim(),
            LastName = options.LastName.Trim(),
            Email = normalizedEmail,
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    options.Password),
            EmailVerifiedAtUtc = now,
            PasswordChangedAtUtc = now,
            SecurityStamp = Guid.NewGuid(),
            IsActive = true,
            RoleId = platformAdminRole.Id,
            Role = platformAdminRole,
            CreatedAt = now
        };

        var membership =
            new OrganizationMembership
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    platformOrganization.Id,
                Organization =
                    platformOrganization,
                UserId = user.Id,
                User = user,
                RoleId = platformAdminRole.Id,
                Role = platformAdminRole,
                IsActive = true,
                IsDefault = true,
                JoinedAtUtc = now
            };

        user.OrganizationMemberships.Add(
            membership);

        await context.Users.AddAsync(user);

        await context.SaveChangesAsync();

        if (!BCrypt.Net.BCrypt.Verify(
                options.Password,
                user.PasswordHash))
        {
            throw new InvalidOperationException(
                "The PlatformAdmin password could not be " +
                "verified after creation.");
        }

        await VerifyPlatformAdmin(
            context,
            platformOrganization.Id,
            platformAdminRole.Id,
            normalizedEmail);
    }

    private static async Task VerifyPlatformAdmin(
        TreasuryDbContext context,
        Guid organizationId,
        Guid roleId,
        string normalizedEmail)
    {
        var isValid =
            await context.OrganizationMemberships
                .AsNoTracking()
                .AnyAsync(membership =>
                    membership.OrganizationId ==
                        organizationId &&
                    membership.RoleId == roleId &&
                    membership.IsActive &&
                    membership.IsDefault &&
                    membership.User.IsActive &&
                    membership.User
                        .EmailVerifiedAtUtc
                        .HasValue &&
                    membership.User.Email ==
                        normalizedEmail &&
                    membership.User.RoleId ==
                        roleId);

        if (!isValid)
        {
            throw new InvalidOperationException(
                "The PlatformAdmin account failed " +
                "post-creation verification.");
        }
    }
}
