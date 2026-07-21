using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.Counterparties;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;

namespace Treasury.Tests.Counterparties;

public class CounterpartyServiceTests
{
    [Fact]
    public async Task Create_NormalizesAndStoresCounterparty()
    {
        var repository =
            new Mock<ICounterpartyRepository>();

        var currentUser =
            new Mock<ICurrentUserService>();

        var auditService =
            new Mock<IAuditLogService>();

        var userId =
            Guid.NewGuid();

        Counterparty? storedCounterparty =
            null;

        currentUser
            .Setup(service =>
                service.UserId)
            .Returns(userId);

        repository
            .Setup(item =>
                item.CodeExists("GTBANK"))
            .ReturnsAsync(false);

        repository
            .Setup(item =>
                item.Add(
                    It.IsAny<Counterparty>()))
            .Callback<Counterparty>(
                counterparty =>
                    storedCounterparty =
                        counterparty)
            .Returns(Task.CompletedTask);

        repository
            .Setup(item =>
                item.SaveChanges())
            .Returns(Task.CompletedTask);

        auditService
            .Setup(item =>
                item.Record(
                    It.IsAny<CreateAuditLogDto>()))
            .Returns(Task.CompletedTask);

        var service =
            new CounterpartyService(
                repository.Object,
                currentUser.Object,
                auditService.Object);

        var result =
            await service.Create(
                new CreateCounterpartyDto
                {
                    Code =
                        " gtbank ",

                    Name =
                        "GTBank Plc",

                    CounterpartyType =
                        "bank",

                    CountryCode =
                        "ng",

                    SwiftCode =
                        "GTBINGLA",

                    CreditRating =
                        "AA",

                    IsActive =
                        true
                });

        Assert.NotNull(storedCounterparty);

        Assert.Equal(
            "GTBANK",
            storedCounterparty.Code);

        Assert.Equal(
            "Bank",
            storedCounterparty
                .CounterpartyType);

        Assert.Equal(
            "NG",
            storedCounterparty.CountryCode);

        Assert.Equal(
            userId,
            storedCounterparty.CreatedByUserId);

        Assert.Equal(
            "GTBANK",
            result.Code);
    }

    [Fact]
    public async Task Create_WhenCodeExists_ThrowsConflict()
    {
        var repository =
            new Mock<ICounterpartyRepository>();

        var currentUser =
            new Mock<ICurrentUserService>();

        var auditService =
            new Mock<IAuditLogService>();

        repository
            .Setup(item =>
                item.CodeExists("GTBANK"))
            .ReturnsAsync(true);

        var service =
            new CounterpartyService(
                repository.Object,
                currentUser.Object,
                auditService.Object);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.Create(
                new CreateCounterpartyDto
                {
                    Code =
                        "GTBANK",

                    Name =
                        "GTBank Plc",

                    CounterpartyType =
                        "Bank",

                    CountryCode =
                        "NG"
                }));
    }
}