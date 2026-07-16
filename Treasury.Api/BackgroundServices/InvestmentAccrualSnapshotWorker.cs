using Microsoft.Extensions.Options;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;

namespace Treasury.Api.BackgroundServices;

public class InvestmentAccrualSnapshotWorker
    : BackgroundService
{
    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<
        InvestmentAccrualSnapshotWorker> _logger;

    private readonly InvestmentAccrualSnapshotWorkerOptions
        _options;

    private readonly TimeSpan _checkInterval;

    private DateTime? _completedSnapshotDateUtc;

    public InvestmentAccrualSnapshotWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<InvestmentAccrualSnapshotWorker> logger,
        IOptions<InvestmentAccrualSnapshotWorkerOptions>
            options)
    {
        _scopeFactory =
            scopeFactory;

        _logger =
            logger;

        _options =
            options.Value;

        _checkInterval =
            TimeSpan.FromMinutes(
                _options.CheckIntervalMinutes);
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Investment accrual snapshot worker " +
                "is disabled.");

            return;
        }

        /*
         * Check immediately on startup. If the application
         * starts after the configured run time, today's
         * snapshot will be generated as a catch-up.
         */
        await GenerateSnapshotIfDue(
            stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    _checkInterval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await GenerateSnapshotIfDue(
                stoppingToken);
        }
    }

    private async Task GenerateSnapshotIfDue(
        CancellationToken stoppingToken)
    {
        var nowUtc =
            DateTime.UtcNow;

        var scheduledTimeUtc =
            nowUtc.Date
                .AddHours(
                    _options.RunHourUtc)
                .AddMinutes(
                    _options.RunMinuteUtc);

        if (nowUtc < scheduledTimeUtc)
        {
            return;
        }

        if (_completedSnapshotDateUtc.HasValue &&
            _completedSnapshotDateUtc.Value ==
                nowUtc.Date)
        {
            return;
        }

        try
        {
            using var scope =
                _scopeFactory.CreateScope();

            var snapshotService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IInvestmentAccrualSnapshotService>();

            var result =
                await snapshotService.Generate(
                    new GenerateInvestmentAccrualSnapshotsDto
                    {
                        SnapshotDateUtc =
                            nowUtc.Date,

                        Currency =
                            _options.Currency,

                        IncludeRedeemed =
                            _options.IncludeRedeemed
                    });

            /*
             * Mark the date as completed even when every
             * eligible row was already saved. That is a
             * successful idempotent execution.
             */
            _completedSnapshotDateUtc =
                nowUtc.Date;

            _logger.LogInformation(
                "Investment accrual snapshot completed " +
                "for {SnapshotDateUtc}. Created " +
                "{CreatedCount}, skipped " +
                "{SkippedCount} existing snapshot(s).",
                result.SnapshotDateUtc,
                result.CreatedSnapshotCount,
                result.SkippedDuplicateCount);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Application is shutting down.
        }
        catch (Exception exception)
        {
            /*
             * Do not mark the date as completed. The worker
             * will retry during the next check interval.
             */
            _logger.LogError(
                exception,
                "Investment accrual snapshot generation " +
                "failed.");
        }
    }
}