using Microsoft.Extensions.Options;
using Treasury.Application.DTOs.TreasuryAlerts;
using Treasury.Application.Interfaces;

namespace Treasury.Api.BackgroundServices;

public class TreasuryAlertMonitoringWorker
    : BackgroundService
{
    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<TreasuryAlertMonitoringWorker>
        _logger;

    private readonly TreasuryAlertMonitoringWorkerOptions
        _options;

    private readonly TimeSpan _interval;

    public TreasuryAlertMonitoringWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<TreasuryAlertMonitoringWorker> logger,
        IOptions<TreasuryAlertMonitoringWorkerOptions> options)
    {
        _scopeFactory =
            scopeFactory;

        _logger =
            logger;

        _options =
            options.Value;

        _interval =
            TimeSpan.FromMinutes(
                _options.IntervalMinutes);
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Treasury alert monitoring worker is disabled.");

            return;
        }

        if (_options.RunOnceOnStartup)
        {
            await RunMonitoringScan(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    _interval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await RunMonitoringScan(stoppingToken);
        }
    }

    private async Task RunMonitoringScan(
        CancellationToken stoppingToken)
    {
        try
        {
            using var scope =
                _scopeFactory.CreateScope();

            var monitoringService =
                scope.ServiceProvider
                    .GetRequiredService<
                        ITreasuryAlertMonitoringService>();

            var result =
                await monitoringService.RunScan(
                    new TreasuryAlertScanRequestDto
                    {
                        LowLiquidityThreshold =
                            _options.LowLiquidityThreshold,

                        ForecastLiquidityThreshold =
                            _options.ForecastLiquidityThreshold,

                        ForecastDays =
                            _options.ForecastDays,

                        PendingApprovalAgeHours =
                            _options.PendingApprovalAgeHours,

                        ReconciliationLookbackDays =
                            _options.ReconciliationLookbackDays,

                        Currency =
                            _options.Currency,

                        IncludeLowLiquidity =
                            _options.IncludeLowLiquidity,

                        IncludeForecastLiquidityGaps =
                            _options.IncludeForecastLiquidityGaps,

                        IncludePendingApprovals =
                            _options.IncludePendingApprovals,

                        IncludeReconciliationExceptions =
                            _options.IncludeReconciliationExceptions
                    });

            if (result.CreatedAlertCount > 0 ||
                result.SkippedDuplicateCount > 0)
            {
                _logger.LogInformation(
                    "Treasury alert monitoring completed. " +
                    "Created {CreatedAlertCount} alert(s), " +
                    "skipped {SkippedDuplicateCount} duplicate(s).",
                    result.CreatedAlertCount,
                    result.SkippedDuplicateCount);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Application is shutting down.
        }
        catch (Exception exception)
        {
            /*
             * Keep the worker alive. A failed scan can be
             * retried during the next interval.
             */
            _logger.LogError(
                exception,
                "Treasury alert monitoring scan failed.");
        }
    }
}