using Moq;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Investments;

public class InvestmentAccrualServiceTests
{
    [Fact]
    public async Task GetReport_ActivePlacement_CalculatesAccruedInterest()
    {
        var placement =
            new InvestmentPlacement
            {
                Id =
                    Guid.NewGuid(),

                Reference =
                    "INV-ACCRUAL-001",

                InstitutionName =
                    "Test Bank",

                Currency =
                    "NGN",

                PrincipalAmount =
                    10_000_000m,

                AnnualInterestRate =
                    10m,

                DayCountBasis =
                    365,

                StartDateUtc =
                    new DateTime(
                        2026,
                        1,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc),

                MaturityDateUtc =
                    new DateTime(
                        2027,
                        1,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc),

                ExpectedInterestAmount =
                    1_000_000m,

                ExpectedMaturityAmount =
                    11_000_000m,

                Status =
                    InvestmentPlacementStatuses.Active
            };

        var repository =
            new Mock<IInvestmentPlacementRepository>();

        repository
            .Setup(item =>
                item.GetForReporting(
                    It.IsAny<
                        InvestmentPortfolioQueryDto>()))
            .ReturnsAsync(
                new List<InvestmentPlacement>
                {
                    placement
                });

        var service =
            new InvestmentAccrualService(
                repository.Object);

        var report =
            await service.GetReport(
                new InvestmentAccrualQueryDto
                {
                    AsOfUtc =
                        new DateTime(
                            2026,
                            7,
                            2,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc),

                    Currency =
                        "NGN"
                });

        var item =
            Assert.Single(report.Items);

        Assert.Equal(
            182,
            item.AccruedDays);

        Assert.Equal(
            498_630.14m,
            item.AccruedInterestAmount);

        Assert.Equal(
            10_498_630.14m,
            item.CarryingAmount);

        Assert.True(
            item.IsOutstandingAsOf);

        Assert.False(
            item.IsRedeemedAsOf);

        var currencySummary =
            Assert.Single(report.Currencies);

        Assert.Equal(
            10_000_000m,
            currencySummary.OutstandingPrincipal);

        Assert.Equal(
            498_630.14m,
            currencySummary.AccruedInterestAmount);

        Assert.Equal(
            10m,
            currencySummary
                .WeightedAverageContractRate);
    }
}