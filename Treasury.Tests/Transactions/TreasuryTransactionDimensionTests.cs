using Moq;
using Treasury.Application.DTOs.Transactions;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Transactions;

public class TreasuryTransactionDimensionTests
{
    [Fact]
    public async Task
        SearchSummaryAndExport_PreserveOrganizationScope()
    {
        var legalEntityId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();
        var transaction =
            new TreasuryTransaction
            {
                Id = Guid.NewGuid(),
                Reference = "TXN-SCOPED",
                TransactionType =
                    TransactionTypes.CashReceipt,
                Status =
                    TransactionStatuses.Completed,
                Amount = 250m,
                Currency = "NGN",
                Description = "Scoped receipt",
                DestinationAccountId =
                    Guid.NewGuid(),
                CreatedAtUtc =
                    DateTime.UtcNow.AddMinutes(-5),
                CompletedAtUtc =
                    DateTime.UtcNow.AddMinutes(-4)
            };
        var repository =
            new Mock<
                ITreasuryTransactionRepository>();

        repository
            .Setup(item =>
                item.Search(
                    It.IsAny<TransactionQueryDto>()))
            .ReturnsAsync(
                (
                    (IReadOnlyList<
                        TreasuryTransaction>)
                        new List<TreasuryTransaction>
                        {
                            transaction
                        },
                    1));
        repository
            .Setup(item =>
                item.GetForActivitySummary(
                    It.IsAny<
                        TreasuryActivitySummaryQueryDto>()))
            .ReturnsAsync(
                new List<TreasuryTransaction>
                {
                    transaction
                });
        repository
            .Setup(item =>
                item.GetForExport(
                    It.IsAny<TransactionQueryDto>(),
                    It.IsAny<int>()))
            .ReturnsAsync(
                new List<TreasuryTransaction>
                {
                    transaction
                });

        var service =
            new TreasuryTransactionService(
                repository.Object);
        var searchQuery =
            new TransactionQueryDto
            {
                LegalEntityId = legalEntityId,
                BusinessUnitId = businessUnitId
            };

        var search =
            await service.SearchTransactions(
                searchQuery);

        Assert.Equal(
            legalEntityId,
            search.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            search.BusinessUnitId);
        Assert.Single(search.Items);

        var activity =
            await service.GetActivitySummary(
                new TreasuryActivitySummaryQueryDto
                {
                    LegalEntityId = legalEntityId,
                    BusinessUnitId = businessUnitId
                });

        Assert.Equal(
            legalEntityId,
            activity.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            activity.BusinessUnitId);
        Assert.Equal(
            1,
            activity.TotalTransactionCount);
        Assert.Equal(
            250m,
            Assert.Single(
                activity.ByCurrency)
                .TotalInflowAmount);

        var export =
            await service.ExportTransactionsCsv(
                searchQuery);
        var csv =
            System.Text.Encoding.UTF8
                .GetString(export.Content);

        Assert.Contains(
            "LegalEntityId,BusinessUnitId",
            csv);
        Assert.Contains(
            legalEntityId.ToString(),
            csv);
        Assert.Contains(
            businessUnitId.ToString(),
            csv);
        Assert.Contains("TXN-SCOPED", csv);
    }

    [Fact]
    public async Task
        Search_EmptyOrganizationDimensionIsRejected()
    {
        var repository =
            new Mock<
                ITreasuryTransactionRepository>();
        var service =
            new TreasuryTransactionService(
                repository.Object);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SearchTransactions(
                new TransactionQueryDto
                {
                    LegalEntityId = Guid.Empty
                }));

        repository.Verify(
            item => item.Search(
                It.IsAny<TransactionQueryDto>()),
            Times.Never);
    }
}
