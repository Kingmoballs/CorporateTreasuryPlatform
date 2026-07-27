using Microsoft.EntityFrameworkCore;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Persistence;

public static class RoleSeeder
{
    public static async Task SeedRoles(
        TreasuryDbContext context)
    {
        var requiredRoleNames = new[]
        {
            Roles.PlatformAdmin,
            Roles.Admin,
            Roles.TreasuryOfficer,
            Roles.FinanceManager,
            Roles.CFO
        };

        var existingRoleNames =
            await context.Roles
                .Select(role => role.Name)
                .ToListAsync();

        var missingRoles = requiredRoleNames
            .Except(
                existingRoleNames,
                StringComparer.OrdinalIgnoreCase)
            .Select(roleName => new Role
            {
                Id = Guid.NewGuid(),
                Name = roleName
            })
            .ToList();

        if (missingRoles.Count == 0)
        {
            return;
        }

        await context.Roles.AddRangeAsync(
            missingRoles);

        await context.SaveChangesAsync();
    }
}