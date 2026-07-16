using Moq;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Infrastructure.Services;

namespace Treasury.Tests.Investments;

public class InvestmentAccrualSnapshotServiceTests
{
    [Fact]
    public async Task Generate_WhenSnapshotExists_SkipsDuplicate()
    {
        var placementId =
            Guid.NewGuid();

        var accrualService =
            new Mock<IInvestmentAccrualService>();

        accrualService
            .Setup(service =>
                service.GetReport(
                    It.IsAny<InvestmentAccrualQueryDto>()))
            .ReturnsAsync(
                new InvestmentAccrualReportDto
                {
                    AsOfUtc =
                        new DateTime(
                            2026,
                            7,
                            16,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc),

                    PlacementCount =
                        1,

                    OutstandingPlacementCount =
                        1,

                    Items =
                    [
                        new InvestmentAccrualItemDto
                        {
                            PlacementId =
                                placementId,

                            Reference =
                                "INV-SNAPSHOT-001",

                            InstitutionName =
                                "Test Bank",

                            Currency =
                                "NGN",

                            Status =
                                "Active",

                            PrincipalAmount =
                                10_000_000m,

                            AnnualInterestRate =
                                10m,

                            DayCountBasis =
                                365,

                            AccruedDays =
                                100,

                            ExpectedInterestAmount =
                                1_000_000m,

                            AccruedInterestAmount =
                                273_972.60m,

                            CarryingAmount =
                                10_273_972.60m,

                            IsOutstandingAsOf =
                                true
                        }
                    ]
                });

        var snapshotRepository =
            new Mock<
                IInvestmentAccrualSnapshotRepository>();

        snapshotRepository
            .Setup(repository =>
                repository.GetExistingPlacementIds(
                    It.IsAny<DateTime>(),
                    It.IsAny<
                        IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(
                new HashSet<Guid>
                {
                    placementId
                });

        var currentUserService =
            new Mock<ICurrentUserService>();

        currentUserService
            .Setup(service =>
                service.UserId)
            .Returns(
                Guid.NewGuid());

        var service =
            new InvestmentAccrualSnapshotService(
                accrualService.Object,
                snapshotRepository.Object,
                currentUserService.Object);

        var result =
            await service.Generate(
                new GenerateInvestmentAccrualSnapshotsDto
                {
                    SnapshotDateUtc =
                        new DateTime(
                            2026,
                            7,
                            16,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc),

                    Currency =
                        "NGN"
                });

        Assert.Equal(
            1,
            result.EligiblePlacementCount);

        Assert.Equal(
            0,
            result.CreatedSnapshotCount);

        Assert.Equal(
            1,
            result.SkippedDuplicateCount);

        Assert.Empty(
            result.CreatedSnapshots);

        snapshotRepository.Verify(
            repository =>
                repository.AddRange(
                    It.IsAny<
                        IReadOnlyCollection<
                            Treasury.Domain.Entities
                                .InvestmentAccrualSnapshot>>()),
            Times.Never);

        snapshotRepository.Verify(
            repository =>
                repository.SaveChanges(),
            Times.Never);
    }
}