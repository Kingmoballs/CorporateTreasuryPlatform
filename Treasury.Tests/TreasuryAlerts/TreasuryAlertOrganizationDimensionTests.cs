using System.Text;
using Moq;
using Treasury.Application.DTOs.TreasuryAlerts;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.TreasuryAlerts;

public class TreasuryAlertOrganizationDimensionTests
{
    [Fact]
    public async Task
        SearchSummaryAndExport_PreserveAccountScope()
    {
        var legalEntityId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();
        var account =
            new Account
            {
                Id = Guid.NewGuid(),
                LegalEntityId = legalEntityId,
                BusinessUnitId = businessUnitId,
                Name = "Scoped alert account",
                AccountNumber = "ALERT-001",
                Currency = "NGN"
            };
        var alert =
            new TreasuryAlert
            {
                Id = Guid.NewGuid(),
                AlertType =
                    TreasuryAlertTypes.LowLiquidity,
                Severity =
                    TreasuryAlertSeverities.Warning,
                Status =
                    TreasuryAlertStatuses.Open,
                Title = "Scoped liquidity alert",
                Message = "Available liquidity is low.",
                AccountId = account.Id,
                Account = account,
                Currency = "NGN",
                CreatedAtUtc = DateTime.UtcNow
            };
        var repository =
            new Mock<ITreasuryAlertRepository>();

        repository
            .Setup(item =>
                item.Search(
                    It.IsAny<TreasuryAlertQueryDto>()))
            .ReturnsAsync(
                (
                    (IReadOnlyList<TreasuryAlert>)
                        new List<TreasuryAlert>
                        {
                            alert
                        },
                    1));
        repository
            .Setup(item =>
                item.GetForSummary(
                    It.IsAny<
                        TreasuryAlertSummaryQueryDto>()))
            .ReturnsAsync(
                new List<TreasuryAlert>
                {
                    alert
                });
        repository
            .Setup(item =>
                item.GetForExport(
                    It.IsAny<TreasuryAlertQueryDto>(),
                    It.IsAny<int>()))
            .ReturnsAsync(
                new List<TreasuryAlert>
                {
                    alert
                });

        var service =
            CreateService(repository.Object);
        var searchQuery =
            new TreasuryAlertQueryDto
            {
                LegalEntityId = legalEntityId,
                BusinessUnitId = businessUnitId
            };
        var search =
            await service.Search(searchQuery);

        Assert.Equal(
            legalEntityId,
            search.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            search.BusinessUnitId);

        var response =
            Assert.Single(search.Items);

        Assert.Equal(
            legalEntityId,
            response.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            response.BusinessUnitId);

        var summary =
            await service.GetSummary(
                new TreasuryAlertSummaryQueryDto
                {
                    LegalEntityId = legalEntityId,
                    BusinessUnitId = businessUnitId
                });

        Assert.Equal(
            legalEntityId,
            summary.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            summary.BusinessUnitId);
        Assert.Single(summary.LatestOpenAlerts);

        var export =
            await service.ExportCsv(searchQuery);
        var csv =
            Encoding.UTF8.GetString(export.Content);

        Assert.Contains(
            "AccountName,LegalEntityId,BusinessUnitId",
            csv);
        Assert.Contains(
            legalEntityId.ToString(),
            csv);
        Assert.Contains(
            businessUnitId.ToString(),
            csv);
        Assert.Contains("Scoped liquidity alert", csv);
    }

    [Fact]
    public async Task
        Search_EmptyOrganizationDimensionIsRejected()
    {
        var repository =
            new Mock<ITreasuryAlertRepository>();
        var service =
            CreateService(repository.Object);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.Search(
                new TreasuryAlertQueryDto
                {
                    BusinessUnitId = Guid.Empty
                }));

        repository.Verify(
            item => item.Search(
                It.IsAny<TreasuryAlertQueryDto>()),
            Times.Never);
    }

    private static TreasuryAlertService CreateService(
        ITreasuryAlertRepository repository)
    {
        return new TreasuryAlertService(
            repository,
            new Mock<IAccountRepository>().Object,
            new Mock<ICurrentUserService>().Object,
            new Mock<IAuditLogService>().Object);
    }
}
