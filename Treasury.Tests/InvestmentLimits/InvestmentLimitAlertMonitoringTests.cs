using Moq;
using Treasury.Application.DTOs.InvestmentLimits;
using Treasury.Application.DTOs.TreasuryAlerts;
using Treasury.Application.Interfaces;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.InvestmentLimits;

public class InvestmentLimitAlertMonitoringTests
{
    [Fact]
    public async Task RunScan_CreatesWarningAndBreachAlerts()
    {
        var accountRepository =
            new Mock<IAccountRepository>();

        var transferRepository =
            new Mock<ITransferRequestRepository>();

        var paymentRepository =
            new Mock<IPaymentRequestRepository>();

        var reversalRepository =
            new Mock<IReversalRequestRepository>();

        var statementRepository =
            new Mock<IBankStatementRepository>();

        var forecastRepository =
            new Mock<ICashFlowForecastRepository>();

        var alertRepository =
            new Mock<ITreasuryAlertRepository>();

        var alertService =
            new Mock<ITreasuryAlertService>();

        var placementRepository =
            new Mock<IInvestmentPlacementRepository>();

        var utilizationService =
            new Mock<
                IInvestmentLimitUtilizationService>();

        var warningLimitId =
            Guid.NewGuid();

        var breachedLimitId =
            Guid.NewGuid();

        utilizationService
            .Setup(service =>
                service.GetUtilization(
                    It.IsAny<
                        InvestmentLimitUtilizationQueryDto>()))
            .ReturnsAsync(
                new InvestmentLimitUtilizationReportDto
                {
                    GeneratedAtUtc =
                        DateTime.UtcNow,

                    EffectiveAtUtc =
                        DateTime.UtcNow,

                    LimitCount =
                        2,

                    WarningCount =
                        1,

                    BreachedCount =
                        1,

                    Items =
                        new List<
                            InvestmentLimitUtilizationItemDto>
                        {
                            new()
                            {
                                InvestmentLimitId =
                                    warningLimitId,

                                CounterpartyId =
                                    Guid.NewGuid(),

                                CounterpartyCode =
                                    "GTBANK",

                                CounterpartyName =
                                    "GTBank",

                                Currency =
                                    "NGN",

                                InvestmentType =
                                    "FixedDeposit",

                                MaximumExposureAmount =
                                    100_000_000m,

                                WarningThresholdPercentage =
                                    80m,

                                WarningThresholdAmount =
                                    80_000_000m,

                                CurrentExposureAmount =
                                    85_000_000m,

                                AvailableLimitAmount =
                                    15_000_000m,

                                UtilizationPercentage =
                                    85m,

                                Status =
                                    InvestmentLimitUtilizationStatuses
                                        .Warning
                            },
                            new()
                            {
                                InvestmentLimitId =
                                    breachedLimitId,

                                CounterpartyId =
                                    Guid.NewGuid(),

                                CounterpartyCode =
                                    "ACCESS",

                                CounterpartyName =
                                    "Access Bank",

                                Currency =
                                    "NGN",

                                InvestmentType =
                                    "All",

                                MaximumExposureAmount =
                                    50_000_000m,

                                WarningThresholdPercentage =
                                    80m,

                                WarningThresholdAmount =
                                    40_000_000m,

                                CurrentExposureAmount =
                                    60_000_000m,

                                AvailableLimitAmount =
                                    0m,

                                BreachAmount =
                                    10_000_000m,

                                UtilizationPercentage =
                                    120m,

                                Status =
                                    InvestmentLimitUtilizationStatuses
                                        .Breached
                            }
                        }
                });

        alertRepository
            .Setup(repository =>
                repository.OpenAlertExists(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string?>()))
            .ReturnsAsync(false);

        alertService
            .Setup(service =>
                service.Create(
                    It.IsAny<CreateTreasuryAlertDto>()))
            .ReturnsAsync(
                (CreateTreasuryAlertDto dto) =>
                    new TreasuryAlertResponseDto
                    {
                        Id =
                            Guid.NewGuid(),

                        AlertType =
                            dto.AlertType,

                        Severity =
                            dto.Severity,

                        Status =
                            TreasuryAlertStatuses.Open,

                        Title =
                            dto.Title,

                        Message =
                            dto.Message,

                        Currency =
                            dto.Currency,

                        SourceModule =
                            dto.SourceModule,

                        SourceEntityType =
                            dto.SourceEntityType,

                        SourceEntityId =
                            dto.SourceEntityId,

                        SourceReference =
                            dto.SourceReference,

                        CreatedAtUtc =
                            DateTime.UtcNow
                    });

        var service =
            new TreasuryAlertMonitoringService(
                accountRepository.Object,
                transferRepository.Object,
                paymentRepository.Object,
                reversalRepository.Object,
                statementRepository.Object,
                forecastRepository.Object,
                alertRepository.Object,
                alertService.Object,
                placementRepository.Object,
                utilizationService.Object);

        var result =
            await service.RunScan(
                new TreasuryAlertScanRequestDto
                {
                    IncludeLowLiquidity =
                        false,

                    IncludeForecastLiquidityGaps =
                        false,

                    IncludePendingApprovals =
                        false,

                    IncludeReconciliationExceptions =
                        false,

                    IncludeInvestmentMaturityAlerts =
                        false,

                    IncludeInvestmentConcentrationAlerts =
                        false,

                    IncludeInvestmentLimitAlerts =
                        true
                });

        Assert.Equal(
            2,
            result.CreatedAlertCount);

        Assert.Equal(
            1,
            result.InvestmentLimitWarningAlertCount);

        Assert.Equal(
            1,
            result.InvestmentLimitBreachAlertCount);

        Assert.Contains(
            result.CreatedAlerts,
            alert =>
                alert.AlertType ==
                    TreasuryAlertTypes
                        .InvestmentLimitWarning);

        Assert.Contains(
            result.CreatedAlerts,
            alert =>
                alert.AlertType ==
                    TreasuryAlertTypes
                        .InvestmentLimitBreach);
    }
}