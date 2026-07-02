using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Tests.Integration;

public sealed class PostgreSqlTestDatabase
    : IAsyncDisposable
{
    private readonly PostgreSqlContainer
        _container;

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
        var options =
            new DbContextOptionsBuilder<
                TreasuryDbContext>()
                .UseNpgsql(
                    _container
                        .GetConnectionString())
                .Options;

        return new TreasuryDbContext(
            options);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}