using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Persistence;

public static class AccountTypeSeeder
{
    public static async Task SeedAccountTypes(
        TreasuryDbContext context)
    {
        if (!context.AccountTypes.Any())
        {
            var accountTypes = 
                new List<AccountType>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = AccountTypes.Operating
                    },

                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = AccountTypes.Payroll
                    },

                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = AccountTypes.Tax
                    },

                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = AccountTypes.Investment
                    },

                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = AccountTypes.Reserve
                    }
                };
            
            await context.AccountTypes
                .AddRangeAsync(accountTypes);
            
            await context.SaveChangesAsync();
        }
    }
}