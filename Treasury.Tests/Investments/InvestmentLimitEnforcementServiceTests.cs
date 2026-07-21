using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.InvestmentLimits;

public class InvestmentLimitEnforcementServiceTests
{
    [Fact]
    public async Task EnsureWithinLimits_WhenProjectedExposureExceedsLimit_Throws()
    {
        var counterpartyRepository =
            new Mock<ICounterpartyRepository>();

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

        var limits =
            new List<InvestmentLimit>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CounterpartyId =
                        counterparty.Id,
                    Currency = "NGN",
                    InvestmentType =
                        InvestmentLimitScopes
                            .AllInvestmentTypes,
                    MaximumExposureAmount =
                        100_000_000m,
                    IsActive = true
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CounterpartyId =
                        counterparty.Id,
                    Currency = "NGN",
                    InvestmentType =
                        InvestmentPlacementTypes
                            .FixedDeposit,
                    MaximumExposureAmount =
                        100_000_000m,
                    IsActive = true
                }
            };

        var existingPlacements =
            new List<InvestmentPlacement>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CounterpartyId =
                        counterparty.Id,
                    Currency = "NGN",
                    InvestmentType =
                        InvestmentPlacementTypes
                            .FixedDeposit,
                    PrincipalAmount =
                        80_000_000m,
                    Status =
                        InvestmentPlacementStatuses.Active
                }
            };

        counterpartyRepository
            .Setup(repository =>
                repository.GetByIdForUpdate(
                    counterparty.Id))
            .ReturnsAsync(counterparty);

        limitRepository
            .Setup(repository =>
                repository
                    .GetApplicableActiveLimitsForUpdate(
                        counterparty.Id,
                        "NGN",
                        InvestmentPlacementTypes
                            .FixedDeposit,
                        It.IsAny<DateTime>()))
            .ReturnsAsync(limits);

        placementRepository
            .Setup(repository =>
                repository.GetForLimitUtilization(
                    counterparty.Id,
                    "NGN"))
            .ReturnsAsync(existingPlacements);

        var service =
            new InvestmentLimitEnforcementService(
                counterpartyRepository.Object,
                limitRepository.Object,
                placementRepository.Object);

        var exception =
            await Assert.ThrowsAsync<
                BusinessRuleException>(
                () => service.EnsureWithinLimits(
                    counterparty.Id,
                    "NGN",
                    InvestmentPlacementTypes
                        .FixedDeposit,
                    30_000_000m,
                    excludedPlacementId: null));

        Assert.Contains(
            "limit exceeded",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureWithinLimits_WhenWithinBothLimits_Succeeds()
    {
        var counterpartyRepository =
            new Mock<ICounterpartyRepository>();

        var limitRepository =
            new Mock<IInvestmentLimitRepository>();

        var placementRepository =
            new Mock<IInvestmentPlacementRepository>();

        var counterparty =
            new Counterparty
            {
                Id = Guid.NewGuid(),
                Code = "ACCESS",
                Name = "Access Bank",
                IsActive = true
            };

        var limits =
            new List<InvestmentLimit>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CounterpartyId =
                        counterparty.Id,
                    Currency = "NGN",
                    InvestmentType = "All",
                    MaximumExposureAmount =
                        100_000_000m,
                    IsActive = true
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CounterpartyId =
                        counterparty.Id,
                    Currency = "NGN",
                    InvestmentType =
                        "FixedDeposit",
                    MaximumExposureAmount =
                        75_000_000m,
                    IsActive = true
                }
            };

        counterpartyRepository
            .Setup(repository =>
                repository.GetByIdForUpdate(
                    counterparty.Id))
            .ReturnsAsync(counterparty);

        limitRepository
            .Setup(repository =>
                repository
                    .GetApplicableActiveLimitsForUpdate(
                        counterparty.Id,
                        "NGN",
                        "FixedDeposit",
                        It.IsAny<DateTime>()))
            .ReturnsAsync(limits);

        placementRepository
            .Setup(repository =>
                repository.GetForLimitUtilization(
                    counterparty.Id,
                    "NGN"))
            .ReturnsAsync(
                new List<InvestmentPlacement>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        CounterpartyId =
                            counterparty.Id,
                        Currency = "NGN",
                        InvestmentType =
                            "FixedDeposit",
                        PrincipalAmount =
                            40_000_000m,
                        Status =
                            InvestmentPlacementStatuses
                                .Active
                    }
                });

        var service =
            new InvestmentLimitEnforcementService(
                counterpartyRepository.Object,
                limitRepository.Object,
                placementRepository.Object);

        await service.EnsureWithinLimits(
            counterparty.Id,
            "NGN",
            "FixedDeposit",
            25_000_000m,
            excludedPlacementId: null);
    }
}