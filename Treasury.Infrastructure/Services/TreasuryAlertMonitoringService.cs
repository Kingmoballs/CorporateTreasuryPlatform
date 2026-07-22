using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.TreasuryAlerts;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.DTOs.InvestmentLimits;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class TreasuryAlertMonitoringService
    : ITreasuryAlertMonitoringService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransferRequestRepository _transferRequestRepository;
    private readonly IPaymentRequestRepository _paymentRequestRepository;
    private readonly IReversalRequestRepository _reversalRequestRepository;
    private readonly IBankStatementRepository _bankStatementRepository;
    private readonly ICashFlowForecastRepository _forecastRepository;
    private readonly ITreasuryAlertRepository _alertRepository;
    private readonly ITreasuryAlertService _alertService;
    private readonly IInvestmentPlacementRepository _investmentPlacementRepository;
    private readonly IInvestmentLimitUtilizationService _investmentLimitUtilizationService;

    public TreasuryAlertMonitoringService(
        IAccountRepository accountRepository,
        ITransferRequestRepository transferRequestRepository,
        IPaymentRequestRepository paymentRequestRepository,
        IReversalRequestRepository reversalRequestRepository,
        IBankStatementRepository bankStatementRepository,
        ICashFlowForecastRepository forecastRepository,
        ITreasuryAlertRepository alertRepository,
        ITreasuryAlertService alertService,
        IInvestmentPlacementRepository investmentPlacementRepository,
        IInvestmentLimitUtilizationService investmentLimitUtilizationService)
    {
        _accountRepository = accountRepository;
        _transferRequestRepository = transferRequestRepository;
        _paymentRequestRepository = paymentRequestRepository;
        _reversalRequestRepository = reversalRequestRepository;
        _bankStatementRepository = bankStatementRepository;
        _forecastRepository = forecastRepository;
        _alertRepository = alertRepository;
        _alertService = alertService;
        _investmentPlacementRepository = investmentPlacementRepository;
        _investmentLimitUtilizationService = investmentLimitUtilizationService;
    }

    public async Task<TreasuryAlertScanResultDto> RunScan(
        TreasuryAlertScanRequestDto request)
    {
        ValidateRequest(request);

        var result =
            new TreasuryAlertScanResultDto
            {
                GeneratedAtUtc =
                    DateTime.UtcNow
            };

        var currency =
            string.IsNullOrWhiteSpace(request.Currency)
                ? null
                : NormalizeCurrency(request.Currency);

        if (request.IncludeLowLiquidity)
        {
            await ScanLowLiquidity(
                request,
                currency,
                result);
        }

        if (request.IncludeForecastLiquidityGaps)
        {
            await ScanForecastLiquidityGaps(
                request,
                currency,
                result);
        }

        if (request.IncludePendingApprovals)
        {
            await ScanPendingApprovals(
                request,
                result);
        }

        if (request.IncludeReconciliationExceptions)
        {
            await ScanReconciliationExceptions(
                request,
                currency,
                result);
        }

        if (request.IncludeInvestmentMaturityAlerts ||
            request.IncludeInvestmentConcentrationAlerts)
        {
            var investmentPlacements =
                await _investmentPlacementRepository.GetForReporting(
                    new InvestmentPortfolioQueryDto
                    {
                        Currency =
                            currency,

                        IncludeRedeemed =
                            false
                    });

            if (request.IncludeInvestmentMaturityAlerts)
            {
                await ScanInvestmentMaturities(
                    request,
                    investmentPlacements,
                    result);
            }

            if (request.IncludeInvestmentConcentrationAlerts)
            {
                await ScanInvestmentConcentration(
                    request,
                    investmentPlacements,
                    result);
            }
        }

        if (request.IncludeInvestmentLimitAlerts)
        {
            await ScanInvestmentLimits(
                currency,
                result);
        }

        result.CreatedAlertCount =
            result.CreatedAlerts.Count;

        return result;
    }

    private async Task ScanLowLiquidity(
        TreasuryAlertScanRequestDto request,
        string? currency,
        TreasuryAlertScanResultDto result)
    {
        var accounts =
            await _accountRepository.GetAll();

        var lowLiquidityAccounts =
            accounts
                .Where(account =>
                    account.IsActive)
                .Where(account =>
                    currency is null ||
                    string.Equals(
                        account.Currency,
                        currency,
                        StringComparison.OrdinalIgnoreCase))
                .Where(account =>
                    account.AvailableBalance <=
                    request.LowLiquidityThreshold)
                .ToList();

        foreach (var account in lowLiquidityAccounts)
        {
            var alert =
                await CreateIfNoOpenDuplicate(
                    new CreateTreasuryAlertDto
                    {
                        AlertType =
                            TreasuryAlertTypes.LowLiquidity,

                        Severity =
                            account.AvailableBalance <= 0
                                ? TreasuryAlertSeverities.Critical
                                : TreasuryAlertSeverities.Warning,

                        Title =
                            $"Low liquidity on {account.Name}",

                        Message =
                            $"Available balance on {account.Name} " +
                            $"is {account.AvailableBalance:N2} " +
                            $"{account.Currency}, below the threshold " +
                            $"of {request.LowLiquidityThreshold:N2}.",

                        AccountId =
                            account.Id,

                        Currency =
                            account.Currency,

                        SourceModule =
                            "Treasury Alert Monitoring",

                        SourceEntityType =
                            AuditEntityTypes.Account,

                        SourceEntityId =
                            account.Id,

                        SourceReference =
                            account.AccountNumber,

                        Metadata =
                            new
                            {
                                account.Balance,
                                account.ReservedBalance,
                                account.AvailableBalance,
                                Threshold =
                                    request.LowLiquidityThreshold
                            }
                    },
                    result);

            if (alert is not null)
            {
                result.LowLiquidityAlertCount++;
            }
        }
    }

    private async Task ScanForecastLiquidityGaps(
        TreasuryAlertScanRequestDto request,
        string? currency,
        TreasuryAlertScanResultDto result)
    {
        var accounts =
            await _accountRepository.GetAll();

        var activeAccounts =
            accounts
                .Where(account =>
                    account.IsActive)
                .Where(account =>
                    currency is null ||
                    string.Equals(
                        account.Currency,
                        currency,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        var fromUtc =
            DateTime.UtcNow.Date;

        var toUtc =
            fromUtc
                .AddDays(request.ForecastDays)
                .AddTicks(-1);

        foreach (var account in activeAccounts)
        {
            var forecastItems =
                await _forecastRepository.GetActiveForPeriod(
                    account.Id,
                    account.Currency,
                    fromUtc,
                    toUtc);

            if (forecastItems.Count == 0)
            {
                continue;
            }

            var projectedBalance =
                account.AvailableBalance;

            var minimumProjectedBalance =
                projectedBalance;

            foreach (var item in forecastItems
                .OrderBy(item => item.ExpectedDateUtc))
            {
                projectedBalance +=
                    item.Direction == CashFlowDirections.Inflow
                        ? item.Amount
                        : -item.Amount;

                minimumProjectedBalance =
                    Math.Min(
                        minimumProjectedBalance,
                        projectedBalance);
            }

            if (minimumProjectedBalance >=
                request.ForecastLiquidityThreshold)
            {
                continue;
            }

            var alert =
                await CreateIfNoOpenDuplicate(
                    new CreateTreasuryAlertDto
                    {
                        AlertType =
                            TreasuryAlertTypes.ForecastLiquidityGap,

                        Severity =
                            minimumProjectedBalance < 0
                                ? TreasuryAlertSeverities.Critical
                                : TreasuryAlertSeverities.Warning,

                        Title =
                            $"Forecast liquidity gap on {account.Name}",

                        Message =
                            $"Projected liquidity on {account.Name} " +
                            $"falls to {minimumProjectedBalance:N2} " +
                            $"{account.Currency} within the next " +
                            $"{request.ForecastDays} day(s).",

                        AccountId =
                            account.Id,

                        Currency =
                            account.Currency,

                        SourceModule =
                            "Cash Flow Forecasts",

                        SourceEntityType =
                            AuditEntityTypes.Account,

                        SourceEntityId =
                            account.Id,

                        SourceReference =
                            $"Forecast-{account.Id}-{fromUtc:yyyyMMdd}-{toUtc:yyyyMMdd}",

                        Metadata =
                            new
                            {
                                OpeningAvailableBalance =
                                    account.AvailableBalance,
                                MinimumProjectedBalance =
                                    minimumProjectedBalance,
                                Threshold =
                                    request.ForecastLiquidityThreshold,
                                ForecastItemCount =
                                    forecastItems.Count,
                                FromUtc =
                                    fromUtc,
                                ToUtc =
                                    toUtc
                            }
                    },
                    result);

            if (alert is not null)
            {
                result.ForecastLiquidityGapAlertCount++;
            }
        }
    }

    private async Task ScanPendingApprovals(
        TreasuryAlertScanRequestDto request,
        TreasuryAlertScanResultDto result)
    {
        var cutoffUtc =
            DateTime.UtcNow.AddHours(
                -request.PendingApprovalAgeHours);

        var transferRequests =
            await _transferRequestRepository.GetPending();

        foreach (var transfer in transferRequests
            .Where(item => item.CreatedAt <= cutoffUtc))
        {
            var alert =
                await CreateIfNoOpenDuplicate(
                    new CreateTreasuryAlertDto
                    {
                        AlertType =
                            TreasuryAlertTypes.PendingApproval,

                        Severity =
                            TreasuryAlertSeverities.Warning,

                        Title =
                            "Transfer approval pending",

                        Message =
                            $"Transfer request {transfer.Id} has been " +
                            $"pending for more than " +
                            $"{request.PendingApprovalAgeHours} hour(s).",

                        SourceModule =
                            "Transfer Approvals",

                        SourceEntityType =
                            AuditEntityTypes.TransferRequest,

                        SourceEntityId =
                            transfer.Id,

                        SourceReference =
                            transfer.Id.ToString(),

                        Metadata =
                            new
                            {
                                transfer.FromAccountId,
                                transfer.ToAccountId,
                                transfer.Amount,
                                transfer.ApprovalCount,
                                transfer.RequiredApprovalCount,
                                transfer.CreatedAt,
                                transfer.ExpiresAtUtc
                            }
                    },
                    result);

            if (alert is not null)
            {
                result.PendingApprovalAlertCount++;
            }
        }

        var paymentRequests =
            await _paymentRequestRepository.GetPending();

        foreach (var payment in paymentRequests
            .Where(item => item.CreatedAtUtc <= cutoffUtc))
        {
            var alert =
                await CreateIfNoOpenDuplicate(
                    new CreateTreasuryAlertDto
                    {
                        AlertType =
                            TreasuryAlertTypes.PendingApproval,

                        Severity =
                            TreasuryAlertSeverities.Warning,

                        Title =
                            "Payment approval pending",

                        Message =
                            $"Payment request {payment.Id} has been " +
                            $"pending for more than " +
                            $"{request.PendingApprovalAgeHours} hour(s).",

                        AccountId =
                            payment.AccountId,

                        Currency =
                            payment.Currency,

                        SourceModule =
                            "Cash Payment Approvals",

                        SourceEntityType =
                            AuditEntityTypes.PaymentRequest,

                        SourceEntityId =
                            payment.Id,

                        SourceReference =
                            payment.Id.ToString(),

                        Metadata =
                            new
                            {
                                payment.Amount,
                                payment.BeneficiaryName,
                                payment.Category,
                                payment.ApprovalCount,
                                payment.RequiredApprovalCount,
                                payment.CreatedAtUtc,
                                payment.ExpiresAtUtc
                            }
                    },
                    result);

            if (alert is not null)
            {
                result.PendingApprovalAlertCount++;
            }
        }

        var reversalRequests =
            await _reversalRequestRepository.GetPending();

        foreach (var reversal in reversalRequests
            .Where(item => item.CreatedAtUtc <= cutoffUtc))
        {
            var alert =
                await CreateIfNoOpenDuplicate(
                    new CreateTreasuryAlertDto
                    {
                        AlertType =
                            TreasuryAlertTypes.PendingApproval,

                        Severity =
                            TreasuryAlertSeverities.Warning,

                        Title =
                            "Reversal approval pending",

                        Message =
                            $"Reversal request {reversal.Id} has been " +
                            $"pending for more than " +
                            $"{request.PendingApprovalAgeHours} hour(s).",

                        SourceModule =
                            "Reversal Approvals",

                        SourceEntityType =
                            AuditEntityTypes.ReversalRequest,

                        SourceEntityId =
                            reversal.Id,

                        SourceReference =
                            reversal.Id.ToString(),

                        Metadata =
                            new
                            {
                                reversal.OriginalTransactionId,
                                reversal.ApprovalCount,
                                reversal.RequiredApprovalCount,
                                reversal.CreatedAtUtc,
                                reversal.ExpiresAtUtc
                            }
                    },
                    result);

            if (alert is not null)
            {
                result.PendingApprovalAlertCount++;
            }
        }
    }

    private async Task ScanReconciliationExceptions(
        TreasuryAlertScanRequestDto request,
        string? currency,
        TreasuryAlertScanResultDto result)
    {
        var fromUtc =
            DateTime.UtcNow
                .AddDays(-request.ReconciliationLookbackDays);

        var unmatchedLines =
            await _bankStatementRepository.GetUnmatchedLines(
                accountId: null,
                fromUtc: fromUtc,
                toUtc: DateTime.UtcNow);

        var filteredLines =
            unmatchedLines
                .Where(line =>
                    currency is null ||
                    string.Equals(
                        line.Currency,
                        currency,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        foreach (var line in filteredLines)
        {
            var alert =
                await CreateIfNoOpenDuplicate(
                    new CreateTreasuryAlertDto
                    {
                        AlertType =
                            TreasuryAlertTypes.ReconciliationException,

                        Severity =
                            TreasuryAlertSeverities.Warning,

                        Title =
                            $"Unmatched bank statement line {line.LineNumber}",

                        Message =
                            $"Bank statement line {line.LineNumber} " +
                            $"for {line.Amount:N2} {line.Currency} " +
                            "is still unmatched.",

                        AccountId =
                            line.AccountId,

                        Currency =
                            line.Currency,

                        SourceModule =
                            "Bank Statement Reconciliation",

                        SourceEntityType =
                            AuditEntityTypes.BankStatementLine,

                        SourceEntityId =
                            line.Id,

                        SourceReference =
                            line.BankReference
                            ?? $"Line {line.LineNumber}",

                        Metadata =
                            new
                            {
                                line.BankStatementImportId,
                                line.LineNumber,
                                line.TransactionDateUtc,
                                line.Description,
                                line.Amount,
                                line.ReconciliationStatus
                            }
                    },
                    result);

            if (alert is not null)
            {
                result.ReconciliationExceptionAlertCount++;
            }
        }
    }

    private async Task ScanInvestmentMaturities(
        TreasuryAlertScanRequestDto request,
        IReadOnlyList<InvestmentPlacement> placements,
        TreasuryAlertScanResultDto result)
    {
        var todayUtc =
            DateTime.UtcNow.Date;

        var warningThroughUtc =
            todayUtc.AddDays(
                request.InvestmentMaturityWarningDays);

        var outstandingPlacements =
            placements
                .Where(placement =>
                    placement.Status ==
                        InvestmentPlacementStatuses.Active ||
                    placement.Status ==
                        InvestmentPlacementStatuses.Matured)
                .ToList();

        foreach (var placement in outstandingPlacements)
        {
            var maturityDateUtc =
                placement.MaturityDateUtc.Date;

            var isOverdue =
                placement.Status ==
                    InvestmentPlacementStatuses.Matured ||
                maturityDateUtc < todayUtc;

            if (isOverdue)
            {
                var overdueAlert =
                    await CreateIfNoOpenDuplicate(
                        new CreateTreasuryAlertDto
                        {
                            AlertType =
                                TreasuryAlertTypes
                                    .InvestmentMaturityOverdue,

                            Severity =
                                TreasuryAlertSeverities.Critical,

                            Title =
                                $"Investment maturity overdue: " +
                                $"{placement.Reference}",

                            Message =
                                $"Investment {placement.Reference} at " +
                                $"{placement.InstitutionName} reached " +
                                $"maturity on " +
                                $"{maturityDateUtc:yyyy-MM-dd} and " +
                                $"remains unredeemed. Expected proceeds " +
                                $"are {placement.ExpectedMaturityAmount:N2} " +
                                $"{placement.Currency}.",

                            AccountId =
                                placement.SourceAccountId,

                            Currency =
                                placement.Currency,

                            SourceModule =
                                "Investment Placements",

                            SourceEntityType =
                                AuditEntityTypes.InvestmentPlacement,

                            SourceEntityId =
                                placement.Id,

                            SourceReference =
                                placement.Reference,

                            Metadata =
                                new
                                {
                                    placement.InstitutionName,
                                    placement.PrincipalAmount,
                                    placement.ExpectedInterestAmount,
                                    placement.ExpectedMaturityAmount,
                                    placement.MaturityDateUtc,
                                    placement.Status
                                }
                        },
                        result);

                if (overdueAlert is not null)
                {
                    result.InvestmentOverdueAlertCount++;
                }

                continue;
            }

            if (maturityDateUtc > warningThroughUtc)
            {
                continue;
            }

            var daysToMaturity =
                (maturityDateUtc - todayUtc).Days;

            var upcomingAlert =
                await CreateIfNoOpenDuplicate(
                    new CreateTreasuryAlertDto
                    {
                        AlertType =
                            TreasuryAlertTypes
                                .InvestmentMaturityUpcoming,

                        Severity =
                            daysToMaturity <= 1
                                ? TreasuryAlertSeverities.Critical
                                : daysToMaturity <= 7
                                    ? TreasuryAlertSeverities.Warning
                                    : TreasuryAlertSeverities.Info,

                        Title =
                            $"Investment maturity approaching: " +
                            $"{placement.Reference}",

                        Message =
                            $"Investment {placement.Reference} at " +
                            $"{placement.InstitutionName} will mature " +
                            $"in {daysToMaturity} day(s), on " +
                            $"{maturityDateUtc:yyyy-MM-dd}. Expected " +
                            $"proceeds are " +
                            $"{placement.ExpectedMaturityAmount:N2} " +
                            $"{placement.Currency}.",

                        AccountId =
                            placement.SourceAccountId,

                        Currency =
                            placement.Currency,

                        SourceModule =
                            "Investment Placements",

                        SourceEntityType =
                            AuditEntityTypes.InvestmentPlacement,

                        SourceEntityId =
                            placement.Id,

                        SourceReference =
                            placement.Reference,

                        Metadata =
                            new
                            {
                                placement.InstitutionName,
                                placement.PrincipalAmount,
                                placement.ExpectedInterestAmount,
                                placement.ExpectedMaturityAmount,
                                placement.MaturityDateUtc,
                                DaysToMaturity =
                                    daysToMaturity
                            }
                    },
                    result);

            if (upcomingAlert is not null)
            {
                result.InvestmentMaturityAlertCount++;
            }
        }
    }

    private async Task ScanInvestmentConcentration(
        TreasuryAlertScanRequestDto request,
        IReadOnlyList<InvestmentPlacement> placements,
        TreasuryAlertScanResultDto result)
    {
        var outstandingPlacements =
            placements
                .Where(placement =>
                    placement.Status ==
                        InvestmentPlacementStatuses.Active ||
                    placement.Status ==
                        InvestmentPlacementStatuses.Matured)
                .Where(placement =>
                    placement.PrincipalAmount > 0)
                .ToList();

        var currencyGroups =
            outstandingPlacements
                .GroupBy(placement =>
                    placement.Currency
                        .Trim()
                        .ToUpperInvariant());

        foreach (var currencyGroup in currencyGroups)
        {
            var totalOutstandingPrincipal =
                currencyGroup.Sum(placement =>
                    placement.PrincipalAmount);

            if (totalOutstandingPrincipal <= 0)
            {
                continue;
            }

            var institutionGroups =
                currencyGroup.GroupBy(
                    placement =>
                        placement.InstitutionName.Trim(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var institutionGroup in institutionGroups)
            {
                var institutionName =
                    institutionGroup.Key;

                var institutionOutstandingPrincipal =
                    institutionGroup.Sum(placement =>
                        placement.PrincipalAmount);

                var concentrationPercentage =
                    Math.Round(
                        institutionOutstandingPrincipal /
                        totalOutstandingPrincipal *
                        100m,
                        2,
                        MidpointRounding.AwayFromZero);

                if (concentrationPercentage <
                    request
                        .InvestmentConcentrationThresholdPercentage)
                {
                    continue;
                }

                var sourceReference =
                    $"InvestmentConcentration-" +
                    $"{currencyGroup.Key}-" +
                    $"{institutionName}";

                /*
                * TreasuryAlert.SourceReference has a maximum
                * database length of 200 characters.
                */
                if (sourceReference.Length > 200)
                {
                    sourceReference =
                        sourceReference[..200];
                }

                var alert =
                    await CreateIfNoOpenDuplicate(
                        new CreateTreasuryAlertDto
                        {
                            AlertType =
                                TreasuryAlertTypes
                                    .InvestmentConcentration,

                            Severity =
                                concentrationPercentage >= 75m
                                    ? TreasuryAlertSeverities.Critical
                                    : TreasuryAlertSeverities.Warning,

                            Title =
                                $"Investment concentration at " +
                                $"{institutionName}",

                            Message =
                                $"{concentrationPercentage:N2}% of the " +
                                $"{currencyGroup.Key} investment " +
                                $"portfolio is placed with " +
                                $"{institutionName}. The configured " +
                                $"threshold is " +
                                $"{request.InvestmentConcentrationThresholdPercentage:N2}%.",

                            Currency =
                                currencyGroup.Key,

                            SourceModule =
                                "Investment Portfolio",

                            SourceEntityType =
                                AuditEntityTypes.System,

                            SourceReference =
                                sourceReference,

                            Metadata =
                                new
                                {
                                    InstitutionName =
                                        institutionName,

                                    TotalOutstandingPrincipal =
                                        totalOutstandingPrincipal,

                                    InstitutionOutstandingPrincipal =
                                        institutionOutstandingPrincipal,

                                    ConcentrationPercentage =
                                        concentrationPercentage,

                                    ThresholdPercentage =
                                        request
                                            .InvestmentConcentrationThresholdPercentage,

                                    PlacementCount =
                                        institutionGroup.Count()
                                }
                        },
                        result);

                if (alert is not null)
                {
                    result.InvestmentConcentrationAlertCount++;
                }
            }
        }
    }

    private async Task ScanInvestmentLimits(
        string? currency,
        TreasuryAlertScanResultDto result)
    {
        var report =
            await _investmentLimitUtilizationService
                .GetUtilization(
                    new InvestmentLimitUtilizationQueryDto
                    {
                        Currency =
                            currency,

                        AsOfUtc =
                            DateTime.UtcNow
                    });

        var reportableItems =
            report.Items
                .Where(item =>
                    item.Status ==
                        InvestmentLimitUtilizationStatuses
                            .Warning ||
                    item.Status ==
                        InvestmentLimitUtilizationStatuses
                            .Breached)
                .ToList();

        foreach (var item in reportableItems)
        {
            var isBreached =
                item.Status ==
                    InvestmentLimitUtilizationStatuses
                        .Breached;

            var sourceReference =
                $"{item.CounterpartyCode}/" +
                $"{item.Currency}/" +
                $"{item.InvestmentType}";

            var alert =
                await CreateIfNoOpenDuplicate(
                    new CreateTreasuryAlertDto
                    {
                        AlertType =
                            isBreached
                                ? TreasuryAlertTypes
                                    .InvestmentLimitBreach
                                : TreasuryAlertTypes
                                    .InvestmentLimitWarning,

                        Severity =
                            isBreached
                                ? TreasuryAlertSeverities
                                    .Critical
                                : TreasuryAlertSeverities
                                    .Warning,

                        Title =
                            isBreached
                                ? $"Investment limit breached: " +
                                $"{sourceReference}"
                                : $"Investment limit warning: " +
                                $"{sourceReference}",

                        Message =
                            isBreached
                                ? $"Investment exposure for " +
                                $"{item.CounterpartyName} is " +
                                $"{item.CurrentExposureAmount:N2} " +
                                $"{item.Currency}, exceeding the " +
                                $"{item.InvestmentType} limit of " +
                                $"{item.MaximumExposureAmount:N2} " +
                                $"by {item.BreachAmount:N2}. " +
                                $"Utilization is " +
                                $"{item.UtilizationPercentage:N2}%."
                                : $"Investment exposure for " +
                                $"{item.CounterpartyName} has " +
                                $"reached " +
                                $"{item.UtilizationPercentage:N2}% " +
                                $"of its {item.InvestmentType} " +
                                $"limit. Current exposure is " +
                                $"{item.CurrentExposureAmount:N2} " +
                                $"{item.Currency}, with " +
                                $"{item.AvailableLimitAmount:N2} " +
                                $"remaining.",

                        Currency =
                            item.Currency,

                        SourceModule =
                            "Investment Limit Monitoring",

                        SourceEntityType =
                            AuditEntityTypes.InvestmentLimit,

                        SourceEntityId =
                            item.InvestmentLimitId,

                        SourceReference =
                            sourceReference,

                        Metadata =
                            new
                            {
                                item.CounterpartyId,
                                item.CounterpartyCode,
                                item.CounterpartyName,
                                item.InvestmentType,
                                item.MaximumExposureAmount,
                                item.WarningThresholdPercentage,
                                item.WarningThresholdAmount,
                                item.PlacementCount,
                                item.CurrentExposureAmount,
                                item.AvailableLimitAmount,
                                item.BreachAmount,
                                item.UtilizationPercentage,
                                item.Status,
                                item.EffectiveFromUtc,
                                item.EffectiveToUtc
                            }
                    },
                    result);

            if (alert is null)
            {
                continue;
            }

            if (isBreached)
            {
                result
                    .InvestmentLimitBreachAlertCount++;
            }
            else
            {
                result
                    .InvestmentLimitWarningAlertCount++;
            }
        }
    }

    private async Task<TreasuryAlertResponseDto?> CreateIfNoOpenDuplicate(
        CreateTreasuryAlertDto dto,
        TreasuryAlertScanResultDto result)
    {
        var duplicateExists =
            await _alertRepository.OpenAlertExists(
                dto.AlertType,
                dto.SourceEntityType,
                dto.SourceEntityId,
                dto.SourceReference);

        if (duplicateExists)
        {
            result.SkippedDuplicateCount++;
            return null;
        }

        var alert =
            await _alertService.Create(dto);

        result.CreatedAlerts.Add(alert);

        return alert;
    }

    private static void ValidateRequest(
        TreasuryAlertScanRequestDto request)
    {
        if (request.LowLiquidityThreshold < 0)
        {
            throw new BusinessRuleException(
                "Low liquidity threshold cannot be negative.");
        }

        if (request.ForecastLiquidityThreshold < 0)
        {
            throw new BusinessRuleException(
                "Forecast liquidity threshold cannot be negative.");
        }

        if (request.ForecastDays < 1 ||
            request.ForecastDays > 180)
        {
            throw new BusinessRuleException(
                "Forecast days must be between 1 and 180.");
        }

        if (request.PendingApprovalAgeHours < 1 ||
            request.PendingApprovalAgeHours > 168)
        {
            throw new BusinessRuleException(
                "Pending approval age must be between 1 and 168 hours.");
        }

        if (request.ReconciliationLookbackDays < 1 ||
            request.ReconciliationLookbackDays > 365)
        {
            throw new BusinessRuleException(
                "Reconciliation lookback days must be between 1 and 365.");
        }

        if (request.InvestmentMaturityWarningDays < 1 ||
            request.InvestmentMaturityWarningDays > 365)
        {
            throw new BusinessRuleException(
                "Investment maturity warning days must be between 1 and 365.");
        }

        if (request.InvestmentConcentrationThresholdPercentage <= 0 ||
            request.InvestmentConcentrationThresholdPercentage > 100)
        {
            throw new BusinessRuleException(
                "Investment concentration threshold must be greater than 0 and not greater than 100.");
        }
    }

    private static string NormalizeCurrency(
        string currency)
    {
        var normalized =
            currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3 ||
            !normalized.All(char.IsLetter))
        {
            throw new BusinessRuleException(
                "Currency must be a valid three-letter code.");
        }

        return normalized;
    }
}