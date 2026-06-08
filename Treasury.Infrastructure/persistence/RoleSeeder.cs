using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Persistence;

public static class RoleSeeder
{
    public static async Task SeedRoles(
        TreasuryDbContext context)
    {
        if (!context.Roles.Any())
        {
            var roles = new List<Role>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = Roles.Admin
                },

                new()
                {
                    Id = Guid.NewGuid(),
                    Name = Roles.TreasuryOfficer
                },

                new()
                {
                    Id = Guid.NewGuid(),
                    Name = Roles.FinanceManager
                },

                new()
                {
                    Id = Guid.NewGuid(),
                    Name = Roles.CFO
                }
            };

            await context.Roles.AddRangeAsync(
                roles);

            await context.SaveChangesAsync();
        }
    }
}