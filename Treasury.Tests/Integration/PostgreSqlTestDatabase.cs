using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Treasury.Application.Interfaces;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public sealed class PostgreSqlTestDatabase
    : IAsyncDisposable
{
    private readonly PostgreSqlContainer
        _container;

    private Guid? _defaultOrganizationId;

    private PostgreSqlTestDatabase(
        PostgreSqlContainer container)
    {
        _container = container;
    }

    public static async Task<PostgreSqlTestDatabase>
        Start()
    {
        /*
         * The image version is pinned so a future
         * PostgreSQL release cannot unexpectedly
         * change test behaviour.
         */
        var container =
            new PostgreSqlBuilder(
                "postgres:16-alpine")
                .Build();

        await container.StartAsync();

        var database =
            new PostgreSqlTestDatabase(
                container);

        try
        {
            await using var context =
                database.CreateContext();

            // Build a fresh database using the real migrations.
            await context.Database
                .MigrateAsync();

            database._defaultOrganizationId =
                await context.Organizations
                    .Where(organization =>
                        organization.Code ==
                            OrganizationDefaults
                                .OrganizationCode)
                    .Select(organization =>
                        organization.Id)
                    .SingleAsync();

            return database;
        }
        catch
        {
            await container.DisposeAsync();
            throw;
        }
    }

    public TreasuryDbContext CreateContext()
    {
        if (!_defaultOrganizationId.HasValue)
        {
            return CreateSystemContext();
        }

        return CreateContext(
            _defaultOrganizationId.Value);
    }

    public TreasuryDbContext CreateContext(
        Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organization ID is required.",
                nameof(organizationId));
        }

        return CreateContext(
            new FixedOrganizationContext(
                organizationId,
                isSystemScope: false));
    }

    public TreasuryDbContext
        CreateContextWithoutOrganization()
    {
        return CreateContext(
            new FixedOrganizationContext(
                organizationId: null,
                isSystemScope: false));
    }

    public TreasuryDbContext CreateSystemContext()
    {
        return CreateContext(
            organizationContext: null);
    }

    private TreasuryDbContext CreateContext(
        IOrganizationContext?
            organizationContext)
    {
        var options =
            new DbContextOptionsBuilder<
                TreasuryDbContext>()
                .UseNpgsql(
                    _container
                        .GetConnectionString())
                .Options;

        return new TreasuryDbContext(
            options,
            organizationContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private sealed class FixedOrganizationContext
        : IOrganizationContext
    {
        public FixedOrganizationContext(
            Guid? organizationId,
            bool isSystemScope)
        {
            OrganizationId =
                organizationId;

            IsSystemScope =
                isSystemScope;
        }

        public Guid? OrganizationId { get; }

        public bool IsSystemScope { get; }
    }
}
