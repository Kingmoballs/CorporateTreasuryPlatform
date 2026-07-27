using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Tests.Tenancy;

public class OrganizationIsolationModelTests
{
    [Fact]
    public void EveryOrganizationOwnedEntity_HasAQueryFilter()
    {
        using var context =
            CreateContext(
                Guid.NewGuid());

        var organizationOwnedTypes =
            context.Model
                .GetEntityTypes()
                .Where(entityType =>
                    typeof(IOrganizationOwnedEntity)
                        .IsAssignableFrom(
                            entityType.ClrType))
                .ToList();

        Assert.Equal(
            32,
            organizationOwnedTypes.Count);

        Assert.All(
            organizationOwnedTypes,
            entityType =>
                Assert.NotEmpty(
                    entityType
                        .GetDeclaredQueryFilters()));
    }

    [Fact]
    public void CrossOrganizationWrite_IsRejectedBeforeDatabaseAccess()
    {
        var currentOrganizationId =
            Guid.NewGuid();

        using var context =
            CreateContext(
                currentOrganizationId);

        context.Accounts.Add(
            new Account
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    Guid.NewGuid(),
                Name = "Cross-tenant account",
                AccountNumber = "CROSS-001",
                Currency = "NGN"
            });

        var exception =
            Assert.Throws<
                UnauthorizedAccessException>(
                () => context.SaveChanges());

        Assert.Contains(
            "another organization",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HttpRequestWithoutOrganization_IsRejected()
    {
        using var context =
            CreateContext(
                organizationId: null);

        context.Accounts.Add(
            new Account
            {
                Id = Guid.NewGuid(),
                Name = "Unscoped account",
                AccountNumber = "UNSCOPED-001",
                Currency = "NGN"
            });

        var exception =
            Assert.Throws<
                UnauthorizedAccessException>(
                () => context.SaveChanges());

        Assert.Contains(
            "organization context",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static TreasuryDbContext CreateContext(
        Guid? organizationId)
    {
        var options =
            new DbContextOptionsBuilder<
                TreasuryDbContext>()
                .UseNpgsql(
                    "Host=localhost;" +
                    "Database=tenant-model;" +
                    "Username=unused;" +
                    "Password=unused")
                .Options;

        return new TreasuryDbContext(
            options,
            new FixedOrganizationContext(
                organizationId));
    }

    private sealed class FixedOrganizationContext
        : IOrganizationContext
    {
        public FixedOrganizationContext(
            Guid? organizationId)
        {
            OrganizationId =
                organizationId;
        }

        public Guid? OrganizationId { get; }

        public bool IsSystemScope => false;
    }
}
