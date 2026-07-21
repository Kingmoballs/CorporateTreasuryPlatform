using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.InvestmentLimits;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;

namespace Treasury.Tests.InvestmentLimits;

public class InvestmentLimitServiceTests
{
    [Fact]
    public async Task Create_WithValidRequest_StoresLimit()
    {
        var limitRepository =
            new Mock<IInvestmentLimitRepository>();

        var counterpartyRepository =
            new Mock<ICounterpartyRepository>();

        var currentUser =
            new Mock<ICurrentUserService>();

        var auditService =
            new Mock<IAuditLogService>();

        var userId = Guid.NewGuid();

        var counterparty =
            new Counterparty
            {
                Id = Guid.NewGuid(),
                Code = "GTBANK",
                Name = "GTBank",
                IsActive = true
            };

        InvestmentLimit? storedLimit = null;

        counterpartyRepository
            .Setup(repository =>
                repository.GetById(
                    counterparty.Id))
            .ReturnsAsync(counterparty);

        limitRepository
            .Setup(repository =>
                repository
                    .HasOverlappingActiveLimit(
                        counterparty.Id,
                        "NGN",
                        "All",
                        It.IsAny<DateTime>(),
                        null,
                        null))
            .ReturnsAsync(false);

        limitRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<InvestmentLimit>()))
            .Callback<InvestmentLimit>(limit =>
                storedLimit = limit)
            .Returns(Task.CompletedTask);

        limitRepository
            .Setup(repository =>
                repository.SaveChanges())
            .Returns(Task.CompletedTask);

        currentUser
            .Setup(service =>
                service.UserId)
            .Returns(userId);

        auditService
            .Setup(service =>
                service.Record(
                    It.IsAny<CreateAuditLogDto>()))
            .Returns(Task.CompletedTask);

        var service =
            new InvestmentLimitService(
                limitRepository.Object,
                counterpartyRepository.Object,
                currentUser.Object,
                auditService.Object);

        var result =
            await service.Create(
                new CreateInvestmentLimitDto
                {
                    CounterpartyId =
                        counterparty.Id,

                    Currency =
                        "ngn",

                    InvestmentType =
                        "all",

                    MaximumExposureAmount =
                        100_000_000m,

                    WarningThresholdPercentage =
                        80m,

                    EffectiveFromUtc =
                        DateTime.UtcNow.Date,

                    IsActive =
                        true
                });

        Assert.NotNull(storedLimit);

        Assert.Equal(
            "NGN",
            storedLimit.Currency);

        Assert.Equal(
            "All",
            storedLimit.InvestmentType);

        Assert.Equal(
            100_000_000m,
            result.MaximumExposureAmount);

        Assert.Equal(
            userId,
            storedLimit.CreatedByUserId);
    }

    [Fact]
    public async Task Create_WhenPeriodOverlaps_ThrowsConflict()
    {
        var limitRepository =
            new Mock<IInvestmentLimitRepository>();

        var counterpartyRepository =
            new Mock<ICounterpartyRepository>();

        var currentUser =
            new Mock<ICurrentUserService>();

        var auditService =
            new Mock<IAuditLogService>();

        var counterparty =
            new Counterparty
            {
                Id = Guid.NewGuid(),
                Code = "GTBANK",
                Name = "GTBank",
                IsActive = true
            };

        counterpartyRepository
            .Setup(repository =>
                repository.GetById(
                    counterparty.Id))
            .ReturnsAsync(counterparty);

        limitRepository
            .Setup(repository =>
                repository
                    .HasOverlappingActiveLimit(
                        counterparty.Id,
                        "NGN",
                        "FixedDeposit",
                        It.IsAny<DateTime>(),
                        null,
                        null))
            .ReturnsAsync(true);

        var service =
            new InvestmentLimitService(
                limitRepository.Object,
                counterpartyRepository.Object,
                currentUser.Object,
                auditService.Object);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.Create(
                new CreateInvestmentLimitDto
                {
                    CounterpartyId =
                        counterparty.Id,

                    Currency =
                        "NGN",

                    InvestmentType =
                        "FixedDeposit",

                    MaximumExposureAmount =
                        50_000_000m,

                    WarningThresholdPercentage =
                        80m,

                    EffectiveFromUtc =
                        DateTime.UtcNow.Date,

                    IsActive =
                        true
                }));
    }
}