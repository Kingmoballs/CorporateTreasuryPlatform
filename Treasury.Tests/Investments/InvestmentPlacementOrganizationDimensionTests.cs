using System.Text;
using Moq;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Investments;

public class InvestmentPlacementOrganizationDimensionTests
{
    [Fact]
    public async Task
        SearchPortfolioScheduleAndExport_PreserveSourceAccountScope()
    {
        var legalEntityId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();
        var account =
            new Account
            {
                Id = Guid.NewGuid(),
                LegalEntityId = legalEntityId,
                BusinessUnitId = businessUnitId,
                Name = "Scoped investment account",
                AccountNumber = "INVEST-001",
                Currency = "NGN"
            };
        var placement =
            CreatePlacement(account);
        var repository =
            new Mock<IInvestmentPlacementRepository>();

        repository
            .Setup(item =>
                item.Search(
                    It.IsAny<
                        InvestmentPlacementQueryDto>()))
            .ReturnsAsync(
                (
                    (IReadOnlyList<
                        InvestmentPlacement>)
                        new List<InvestmentPlacement>
                        {
                            placement
                        },
                    1));
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
            CreateService(repository.Object);
        var search =
            await service.Search(
                new InvestmentPlacementQueryDto
                {
                    LegalEntityId = legalEntityId,
                    BusinessUnitId = businessUnitId
                });

        Assert.Equal(
            legalEntityId,
            search.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            search.BusinessUnitId);

        var placementResponse =
            Assert.Single(search.Items);

        Assert.Equal(
            legalEntityId,
            placementResponse.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            placementResponse.BusinessUnitId);

        var portfolioQuery =
            new InvestmentPortfolioQueryDto
            {
                LegalEntityId = legalEntityId,
                BusinessUnitId = businessUnitId
            };
        var report =
            await service.GetPortfolioReport(
                portfolioQuery);
        var schedule =
            await service.GetMaturitySchedule(
                portfolioQuery);

        Assert.Equal(
            legalEntityId,
            report.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            report.BusinessUnitId);
        Assert.Equal(1, report.PlacementCount);
        Assert.Equal(
            legalEntityId,
            Assert.Single(schedule.Items)
                .LegalEntityId);
        Assert.Equal(
            businessUnitId,
            schedule.BusinessUnitId);

        var export =
            await service.ExportPortfolioCsv(
                portfolioQuery);
        var csv =
            Encoding.UTF8.GetString(export.Content);

        Assert.Contains(
            "SourceAccountId,LegalEntityId,BusinessUnitId",
            csv);
        Assert.Contains(
            legalEntityId.ToString(),
            csv);
        Assert.Contains(
            businessUnitId.ToString(),
            csv);
        Assert.Contains(
            placement.Reference,
            csv);
    }

    [Fact]
    public async Task
        Search_EmptyOrganizationDimensionIsRejected()
    {
        var repository =
            new Mock<IInvestmentPlacementRepository>();
        var service =
            CreateService(repository.Object);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.Search(
                new InvestmentPlacementQueryDto
                {
                    LegalEntityId = Guid.Empty
                }));

        repository.Verify(
            item => item.Search(
                It.IsAny<
                    InvestmentPlacementQueryDto>()),
            Times.Never);
    }

    private static InvestmentPlacement
        CreatePlacement(Account account)
    {
        var startDateUtc =
            DateTime.UtcNow.Date;

        return new InvestmentPlacement
        {
            Id = Guid.NewGuid(),
            Reference = "INV-SCOPED",
            InvestmentType =
                InvestmentPlacementTypes.FixedDeposit,
            InstitutionName = "Scoped Bank",
            SourceAccountId = account.Id,
            SourceAccount = account,
            PrincipalAmount = 1_000m,
            Currency = account.Currency,
            AnnualInterestRate = 10m,
            DayCountBasis = 365,
            StartDateUtc = startDateUtc,
            MaturityDateUtc =
                startDateUtc.AddDays(30),
            ExpectedInterestAmount = 10m,
            ExpectedMaturityAmount = 1_010m,
            Status =
                InvestmentPlacementStatuses.Active,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static InvestmentPlacementService
        CreateService(
            IInvestmentPlacementRepository repository)
    {
        return new InvestmentPlacementService(
            repository,
            new Mock<ICounterpartyRepository>().Object,
            new Mock<IAccountRepository>().Object,
            new Mock<
                ITreasuryTransactionRepository>().Object,
            new Mock<ILedgerRepository>().Object,
            new Mock<
                ICashFlowForecastRepository>().Object,
            new Mock<ICurrentUserService>().Object,
            new Mock<IAuditLogService>().Object,
            new Mock<IApprovalPolicyService>().Object,
            new Mock<
                IApprovalDecisionRepository>().Object,
            new Mock<
                IInvestmentLimitEnforcementService>()
                .Object);
    }
}
