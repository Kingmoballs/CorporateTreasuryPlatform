using Microsoft.Extensions.Options;
using Treasury.Application.Interfaces;

namespace Treasury.Api.BackgroundServices;

public class PendingRequestExpiryWorker
    : BackgroundService
{
    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<
        PendingRequestExpiryWorker> _logger;

    private readonly TimeSpan _interval;

    public PendingRequestExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingRequestExpiryWorker> logger,
        IOptions<PendingRequestExpiryWorkerOptions>
            options)
    {
        _scopeFactory =
            scopeFactory;

        _logger =
            logger;

        _interval =
            TimeSpan.FromMinutes(
                options.Value.IntervalMinutes);
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope =
                    _scopeFactory.CreateScope();

                var expiryService =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IPendingRequestExpiryService>();

                var result =
                    await expiryService
                        .ExpireDueRequests(
                            stoppingToken);

                if (result.TotalExpiredCount > 0)
                {
                    _logger.LogInformation(
                        "Expired {TransferCount} transfers, " +
                        "{PaymentCount} payments and " +
                        "{ReversalCount} reversals.",
                        result.ExpiredTransferCount,
                        result.ExpiredPaymentCount,
                        result.ExpiredReversalCount);
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
                /*
                 * Keep the worker alive. A concurrency
                 * conflict can be retried next interval.
                 */
                _logger.LogError(
                    exception,
                    "Pending request expiration failed.");
            }

            try
            {
                await Task.Delay(
                    _interval,
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
}