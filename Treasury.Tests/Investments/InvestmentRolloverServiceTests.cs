using Moq;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Investments;

public class InvestmentRolloverServiceTests
{
    [Fact]
    public async Task GetQuote_PrincipalAndNetInterest_ReinvestsAllNetProceeds()
    {
        var todayUtc =
            DateTime.UtcNow.Date;

        var placement =
            CreateMaturedPlacement(todayUtc);

        var repository =
            CreateRepository(placement);

        var service =
            new InvestmentRolloverService(
                repository.Object);

        var result =
            await service.GetQuote(
                placement.Id,
                CreateRequest(
                    todayUtc,
                    InvestmentRolloverOptions
                        .PrincipalAndNetInterest));

        Assert.Equal(
            100_000m,
            result.WithholdingTaxAmount);

        Assert.Equal(
            900_000m,
            result.NetInterestAmount);

        Assert.Equal(
            10_900_000m,
            result.RolloverPrincipalAmount);

        Assert.Equal(
            0m,
            result.CashPayoutAmount);

        Assert.Equal(
            1_308_000m,
            result.NewExpectedInterestAmount);

        Assert.Equal(
            12_208_000m,
            result.NewExpectedMaturityAmount);

        Assert.True(
            result.CanExecuteNow);
    }

    [Fact]
    public async Task GetQuote_PrincipalOnly_LeavesNetInterestForCashPayout()
    {
        var todayUtc =
            DateTime.UtcNow.Date;

        var placement =
            CreateMaturedPlacement(todayUtc);

        var repository =
            CreateRepository(placement);

        var service =
            new InvestmentRolloverService(
                repository.Object);

        var result =
            await service.GetQuote(
                placement.Id,
                CreateRequest(
                    todayUtc,
                    InvestmentRolloverOptions
                        .PrincipalOnly));

        Assert.Equal(
            10_000_000m,
            result.RolloverPrincipalAmount);

        Assert.Equal(
            900_000m,
            result.CashPayoutAmount);

        Assert.Equal(
            1_200_000m,
            result.NewExpectedInterestAmount);

        Assert.Equal(
            11_200_000m,
            result.NewExpectedMaturityAmount);
    }

    private static InvestmentPlacement
        CreateMaturedPlacement(
            DateTime maturityDateUtc)
    {
        return new InvestmentPlacement
        {
            Id =
                Guid.NewGuid(),

            Reference =
                "INV-ROLLOVER-001",

            InvestmentType =
                InvestmentPlacementTypes.FixedDeposit,

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
                maturityDateUtc.AddDays(-365),

            MaturityDateUtc =
                maturityDateUtc,

            ExpectedInterestAmount =
                1_000_000m,

            ExpectedMaturityAmount =
                11_000_000m,

            Status =
                InvestmentPlacementStatuses.Matured
        };
    }

    private static Mock<IInvestmentPlacementRepository>
        CreateRepository(
            InvestmentPlacement placement)
    {
        var repository =
            new Mock<IInvestmentPlacementRepository>();

        repository
            .Setup(item =>
                item.GetById(
                    placement.Id))
            .ReturnsAsync(
                placement);

        return repository;
    }

    private static InvestmentRolloverQuoteRequestDto
        CreateRequest(
            DateTime startDateUtc,
            string rolloverOption)
    {
        return new InvestmentRolloverQuoteRequestDto
        {
            RolloverOption =
                rolloverOption,

            GrossInterestAmount =
                1_000_000m,

            WithholdingTaxRatePercentage =
                10m,

            NewInvestmentType =
                InvestmentPlacementTypes.FixedDeposit,

            NewInstitutionName =
                "Test Bank",

            NewAnnualInterestRate =
                12m,

            NewDayCountBasis =
                365,

            NewStartDateUtc =
                startDateUtc,

            NewMaturityDateUtc =
                startDateUtc.AddDays(365)
        };
    }
}