using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Api.HealthChecks;

public class DatabaseReadinessHealthCheck
    : IHealthCheck
{
    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<
        DatabaseReadinessHealthCheck> _logger;

    public DatabaseReadinessHealthCheck(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseReadinessHealthCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult>
        CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken =
                default)
    {
        try
        {
            using var scope =
                _scopeFactory.CreateScope();
            var database =
                scope.ServiceProvider
                    .GetRequiredService<
                        TreasuryDbContext>();

            return await database.Database
                .CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy(
                    "The database is reachable.")
                : HealthCheckResult.Unhealthy(
                    "The database is unreachable.");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Database readiness check failed.");

            return HealthCheckResult.Unhealthy(
                "The database readiness check failed.");
        }
    }
}
