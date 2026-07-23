using Microsoft.Extensions.Options;
using Treasury.Application.Interfaces;

namespace Treasury.Api.BackgroundServices;

public class
    AuthenticationSecurityEventRetentionWorker
    : BackgroundService
{
    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<
        AuthenticationSecurityEventRetentionWorker>
        _logger;

    private readonly TimeProvider _timeProvider;

    private readonly
        AuthenticationSecurityEventRetentionOptions
        _options;

    public AuthenticationSecurityEventRetentionWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<
            AuthenticationSecurityEventRetentionWorker>
            logger,
        TimeProvider timeProvider,
        IOptions<
            AuthenticationSecurityEventRetentionOptions>
            options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await DeleteExpiredEvents(
                    stoppingToken);

                if (deleted > 0)
                {
                    _logger.LogInformation(
                        "Deleted {EventCount} expired " +
                        "authentication security events.",
                        deleted);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken
                    .IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Authentication security-event " +
                    "retention failed.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromHours(
                        _options.IntervalHours),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken
                    .IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task<int> DeleteExpiredEvents(
        CancellationToken stoppingToken)
    {
        var cutoffUtc =
            _timeProvider
                .GetUtcNow()
                .UtcDateTime
                .AddDays(
                    -_options.RetentionDays);

        var totalDeleted = 0;

        while (!stoppingToken
                   .IsCancellationRequested)
        {
            using var scope =
                _scopeFactory.CreateScope();

            var service =
                scope.ServiceProvider
                    .GetRequiredService<
                        IAuthenticationSecurityEventService>();

            var deleted =
                await service.DeleteOlderThan(
                    cutoffUtc,
                    _options.BatchSize);

            totalDeleted += deleted;

            if (deleted < _options.BatchSize)
            {
                break;
            }
        }

        return totalDeleted;
    }
}
