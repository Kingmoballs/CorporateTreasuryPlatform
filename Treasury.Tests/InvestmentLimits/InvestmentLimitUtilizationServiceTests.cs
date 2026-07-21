using Moq;
using Treasury.Application.DTOs.InvestmentLimits;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.InvestmentLimits;

public class InvestmentLimitUtilizationServiceTests
{
    [Fact]
    public async Task GetUtilization_WhenThresholdReached_ReturnsWarning()
    {
        var limitRepository =
            new Mock<IInvestmentLimitRepository>();

        var placementRepository =
            new Mock<IInvestmentPlacementRepository>();

        var counterparty =
            new Counterparty
            {
                Id = Guid.NewGuid(),
                Code = "GTBANK",
                Name = "GTBank",
                IsActive = true
            };

        var limit =
            new InvestmentLimit
            {
                Id = Guid.NewGuid(),
                CounterpartyId = counterparty.Id,
                Counterparty = counterparty,
                Currency = "NGN",
                InvestmentType =
                    InvestmentLimitScopes
                        .AllInvestmentTypes,
                MaximumExposureAmount =
                    100_000_000m,
                WarningThresholdPercentage =
                    80m,
                IsActive = true,
                EffectiveFromUtc =
                    DateTime.UtcNow.AddDays(-1)
            };

        var placements =
            new List<InvestmentPlacement>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CounterpartyId =
                        counterparty.Id,
                    Counterparty =
                        counterparty,
                    InstitutionName =
                        counterparty.Name,
                    Currency =
                        "NGN",
                    InvestmentType =
                        InvestmentPlacementTypes
                            .FixedDeposit,
                    PrincipalAmount =
                        90_000_000m,
                    Status =
                        InvestmentPlacementStatuses.Active
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CounterpartyId =
                        null,
                    InstitutionName =
                        "Legacy Bank",
                    Currency =
                        "NGN",
                    InvestmentType =
                        InvestmentPlacementTypes
                            .FixedDeposit,
                    PrincipalAmount =
                        5_000_000m,
                    Status =
                        InvestmentPlacementStatuses.Active
                }
            };

        limitRepository
            .Setup(repository =>
                repository.GetApplicableActiveLimits(
                    null,
                    "NGN",
                    It.IsAny<DateTime>()))
            .ReturnsAsync(
                new List<InvestmentLimit>
                {
                    limit
                });

        placementRepository
            .Setup(repository =>
                repository.GetForLimitUtilization(
                    null,
                    "NGN"))
            .ReturnsAsync(placements);

        var service =
            new InvestmentLimitUtilizationService(
                limitRepository.Object,
                placementRepository.Object);

        var result =
            await service.GetUtilization(
                new InvestmentLimitUtilizationQueryDto
                {
                    Currency = "ngn"
                });

        var item =
            Assert.Single(result.Items);

        Assert.Equal(
            90_000_000m,
            item.CurrentExposureAmount);

        Assert.Equal(
            10_000_000m,
            item.AvailableLimitAmount);

        Assert.Equal(
            90m,
            item.UtilizationPercentage);

        Assert.Equal(
            InvestmentLimitUtilizationStatuses.Warning,
            item.Status);

        Assert.Equal(
            1,
            result.UnassignedPlacementCount);
    }
}