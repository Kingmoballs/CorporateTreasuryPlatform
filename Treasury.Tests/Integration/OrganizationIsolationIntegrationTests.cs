using Microsoft.EntityFrameworkCore;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Repositories;

namespace Treasury.Tests.Integration;

public class OrganizationIsolationIntegrationTests
{
    [Fact]
    public async Task AccountQueriesAndNaturalKeys_AreTenantScoped()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        Guid firstOrganizationId;
        Guid secondOrganizationId;
        Guid firstAccountId;
        Guid secondAccountId;
        Guid accountTypeId;

        await using (var context =
            database.CreateContext())
        {
            firstOrganizationId =
                context.CurrentOrganizationId!.Value;

            var secondOrganization =
                new Organization
                {
                    Id = Guid.NewGuid(),
                    Code = "SECOND",
                    Name = "Second Organization",
                    Slug = "second-organization",
                    CountryCode = "NG",
                    BaseCurrency = "NGN"
                };

            var accountType =
                new AccountType
                {
                    Id = Guid.NewGuid(),
                    Name = "Tenant Isolation Test"
                };

            context.Organizations.Add(
                secondOrganization);

            context.AccountTypes.Add(
                accountType);

            await context.SaveChangesAsync();

            secondOrganizationId =
                secondOrganization.Id;

            accountTypeId =
                accountType.Id;

            var firstAccount =
                CreateAccount(
                    accountTypeId);

            context.Accounts.Add(
                firstAccount);

            await context.SaveChangesAsync();

            firstAccountId =
                firstAccount.Id;

            Assert.Equal(
                firstOrganizationId,
                firstAccount.OrganizationId);
        }

        await using (var context =
            database.CreateContext(
                secondOrganizationId))
        {
            var secondAccount =
                CreateAccount(
                    accountTypeId);

            context.Accounts.Add(
                secondAccount);

            await context.SaveChangesAsync();

            secondAccountId =
                secondAccount.Id;

            Assert.Equal(
                secondOrganizationId,
                secondAccount.OrganizationId);
        }

        await using (var context =
            database.CreateContext(
                firstOrganizationId))
        {
            var repository =
                new AccountRepository(context);

            Assert.NotNull(
                await repository.GetById(
                    firstAccountId));

            Assert.Null(
                await repository.GetById(
                    secondAccountId));
        }

        await using (var context =
            database.CreateContext(
                secondOrganizationId))
        {
            var repository =
                new AccountRepository(context);

            Assert.NotNull(
                await repository.GetById(
                    secondAccountId));

            Assert.Null(
                await repository.GetById(
                    firstAccountId));
        }

        await using (var context =
            database
                .CreateContextWithoutOrganization())
        {
            Assert.Empty(
                await context.Accounts
                    .ToListAsync());
        }
    }

    private static Account CreateAccount(
        Guid accountTypeId)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = "Shared Number Account",
            AccountNumber = "SHARED-001",
            Currency = "NGN",
            Balance = 1_000m,
            AccountTypeId = accountTypeId,
            IsActive = true,
            ConcurrencyToken = Guid.NewGuid()
        };
    }
}
