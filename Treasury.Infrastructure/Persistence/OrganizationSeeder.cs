using Microsoft.EntityFrameworkCore;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Persistence;

public static class OrganizationSeeder
{
    public static async Task Seed(
        TreasuryDbContext context)
    {
        var organization =
            await context.Organizations
                .FirstOrDefaultAsync(item =>
                    item.Code ==
                    OrganizationDefaults
                        .OrganizationCode);

        if (organization == null)
        {
            organization = new Organization
            {
                Id = Guid.NewGuid(),
                Code = OrganizationDefaults
                    .OrganizationCode,
                Name = OrganizationDefaults
                    .OrganizationName,
                Slug = OrganizationDefaults
                    .OrganizationSlug,
                CountryCode = OrganizationDefaults
                    .CountryCode,
                BaseCurrency = OrganizationDefaults
                    .BaseCurrency
            };

            await context.Organizations.AddAsync(
                organization);

            await context.SaveChangesAsync();
        }

        var legalEntity =
            await context.LegalEntities
                .FirstOrDefaultAsync(item =>
                    item.OrganizationId ==
                        organization.Id &&
                    item.Code ==
                        OrganizationDefaults
                            .LegalEntityCode);

        if (legalEntity == null)
        {
            legalEntity = new LegalEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                Organization = organization,
                Code = OrganizationDefaults
                    .LegalEntityCode,
                Name = OrganizationDefaults
                    .LegalEntityName,
                CountryCode = OrganizationDefaults
                    .CountryCode,
                BaseCurrency = OrganizationDefaults
                    .BaseCurrency
            };

            await context.LegalEntities.AddAsync(
                legalEntity);

            await context.SaveChangesAsync();
        }

        var hasBusinessUnit =
            await context.BusinessUnits
                .AnyAsync(item =>
                    item.OrganizationId ==
                        organization.Id &&
                    item.Code ==
                        OrganizationDefaults
                            .BusinessUnitCode);

        if (!hasBusinessUnit)
        {
            await context.BusinessUnits.AddAsync(
                new BusinessUnit
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        organization.Id,
                    Organization = organization,
                    LegalEntityId =
                        legalEntity.Id,
                    LegalEntity = legalEntity,
                    Code = OrganizationDefaults
                        .BusinessUnitCode,
                    Name = OrganizationDefaults
                        .BusinessUnitName
                });

            await context.SaveChangesAsync();
        }

        var usersWithMembership =
            await context.OrganizationMemberships
                .Select(membership =>
                    membership.UserId)
                .ToHashSetAsync();

        var existingUsers =
            await context.Users
                .Where(user =>
                    !usersWithMembership.Contains(
                        user.Id))
                .ToListAsync();

        if (existingUsers.Count == 0)
        {
            return;
        }

        var memberships = existingUsers
            .Select(user =>
                new OrganizationMembership
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        organization.Id,
                    UserId = user.Id,
                    RoleId = user.RoleId,
                    IsActive = user.IsActive,
                    IsDefault = true
                })
            .ToList();

        await context.OrganizationMemberships
            .AddRangeAsync(memberships);

        await context.SaveChangesAsync();
    }
}
