using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Treasury.Api.HealthChecks;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Tests.Integration;

public class DeploymentReadinessIntegrationTests
{
    [Fact]
    public async Task
        DatabaseReadiness_ReachablePostgreSqlIsHealthy()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<TreasuryDbContext>(
            _ => database.CreateContext());
        await using var provider =
            services.BuildServiceProvider();
        var check =
            new DatabaseReadinessHealthCheck(
                provider.GetRequiredService<
                    IServiceScopeFactory>(),
                provider.GetRequiredService<
                    ILogger<
                        DatabaseReadinessHealthCheck>>());

        var result = await check.CheckHealthAsync(
            new HealthCheckContext());

        Assert.Equal(
            HealthStatus.Healthy,
            result.Status);
    }
}
