using Moq;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Investments;

public class InvestmentEarlyRedemptionServiceTests
{
    [Fact]
    public async Task GetQuote_ActivePlacement_CalculatesNetProceeds()
    {
        var todayUtc =
            DateTime.UtcNow.Date;

        var placement =
            new InvestmentPlacement
            {
                Id =
                    Guid.NewGuid(),

                Reference =
                    "INV-EARLY-001",

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
                    todayUtc.AddDays(-100),

                MaturityDateUtc =
                    todayUtc.AddDays(100),

                ExpectedInterestAmount =
                    547_945.21m,

                ExpectedMaturityAmount =
                    10_547_945.21m,

                Status =
                    InvestmentPlacementStatuses.Active
            };

        var repository =
            new Mock<IInvestmentPlacementRepository>();

        repository
            .Setup(item =>
                item.GetById(
                    placement.Id))
            .ReturnsAsync(
                placement);

        var service =
            new InvestmentEarlyRedemptionService(
                repository.Object);

        var result =
            await service.GetQuote(
                placement.Id,
                new
                    InvestmentEarlyRedemptionQuoteRequestDto
                    {
                        ProposedRedemptionDateUtc =
                            todayUtc,

                        PenaltyRatePercentage =
                            20m,

                        WithholdingTaxRatePercentage =
                            10m
                    });

        Assert.Equal(
            100,
            result.InvestedDays);

        Assert.Equal(
            273_972.60m,
            result.GrossAccruedInterestAmount);

        Assert.Equal(
            54_794.52m,
            result.PenaltyAmount);

        Assert.Equal(
            219_178.08m,
            result.InterestAfterPenaltyAmount);

        Assert.Equal(
            21_917.81m,
            result.WithholdingTaxAmount);

        Assert.Equal(
            197_260.27m,
            result.NetInterestAmount);

        Assert.Equal(
            10_197_260.27m,
            result.EstimatedRedemptionProceeds);
    }
}