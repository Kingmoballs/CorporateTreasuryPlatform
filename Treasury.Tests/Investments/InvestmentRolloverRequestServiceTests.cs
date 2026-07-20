using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.ApprovalPolicies;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Investments;

public class InvestmentRolloverRequestServiceTests
{
    [Fact]
    public async Task Create_PrincipalOnly_PersistsLockedQuote()
    {
        var placementId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var payoutAccountId = Guid.NewGuid();

        var requestRepository =
            new Mock<IInvestmentRolloverRequestRepository>();

        requestRepository
            .Setup(repository =>
                repository.GetByIdempotencyKey(
                    "rollover-request-001"))
            .ReturnsAsync(
                (InvestmentRolloverRequest?)null);

        requestRepository
            .Setup(repository =>
                repository.GetOpenForPlacement(
                    placementId))
            .ReturnsAsync(
                (InvestmentRolloverRequest?)null);

        InvestmentRolloverRequest? savedRequest = null;

        requestRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<InvestmentRolloverRequest>()))
            .Callback<InvestmentRolloverRequest>(
                request => savedRequest = request)
            .Returns(Task.CompletedTask);

        requestRepository
            .Setup(repository =>
                repository.SaveChanges())
            .Returns(Task.CompletedTask);

        var quoteService =
            new Mock<IInvestmentRolloverService>();

        quoteService
            .Setup(service =>
                service.GetQuote(
                    placementId,
                    It.IsAny<
                        InvestmentRolloverQuoteRequestDto>()))
            .ReturnsAsync(
                new InvestmentRolloverQuoteDto
                {
                    OriginalInvestmentPlacementId =
                        placementId,

                    OriginalInvestmentReference =
                        "INV-ROLLOVER-001",

                    OriginalInstitutionName =
                        "Test Bank",

                    Currency =
                        "NGN",

                    OriginalMaturityDateUtc =
                        DateTime.UtcNow.Date,

                    OriginalPrincipalAmount =
                        10_000_000m,

                    GrossInterestAmount =
                        1_000_000m,

                    GrossMaturityAmount =
                        11_000_000m,

                    WithholdingTaxRatePercentage =
                        10m,

                    WithholdingTaxAmount =
                        100_000m,

                    NetInterestAmount =
                        900_000m,

                    NetMaturityProceeds =
                        10_900_000m,

                    RolloverOption =
                        InvestmentRolloverOptions
                            .PrincipalOnly,

                    RolloverPrincipalAmount =
                        10_000_000m,

                    CashPayoutAmount =
                        900_000m,

                    NewInvestmentType =
                        InvestmentPlacementTypes
                            .FixedDeposit,

                    NewInstitutionName =
                        "Test Bank",

                    NewAnnualInterestRate =
                        12m,

                    NewDayCountBasis =
                        365,

                    NewStartDateUtc =
                        DateTime.UtcNow.Date,

                    NewMaturityDateUtc =
                        DateTime.UtcNow.Date
                            .AddDays(365),

                    NewTenorDays =
                        365,

                    NewExpectedInterestAmount =
                        1_200_000m,

                    NewExpectedMaturityAmount =
                        11_200_000m
                });

        var accountRepository =
            new Mock<IAccountRepository>();

        accountRepository
            .Setup(repository =>
                repository.GetById(
                    payoutAccountId))
            .ReturnsAsync(
                new Account
                {
                    Id =
                        payoutAccountId,

                    Name =
                        "Operating Account",

                    Currency =
                        "NGN",

                    IsActive =
                        true
                });

        var approvalPolicyService =
            new Mock<IApprovalPolicyService>();

        approvalPolicyService
            .Setup(service =>
                service.GetRequirements(
                    ApprovalOperationTypes
                        .InvestmentRollover,
                    "NGN"))
            .ReturnsAsync(
                new ApprovalRequirementsDto
                {
                    RequiredApprovalCount =
                        2,

                    PendingRequestExpiryHours =
                        24
                });

        var currentUserService =
            new Mock<ICurrentUserService>();

        currentUserService
            .Setup(service =>
                service.UserId)
            .Returns(
                requesterId);

        var auditLogService =
            new Mock<IAuditLogService>();

        auditLogService
            .Setup(service =>
                service.Record(
                    It.IsAny<CreateAuditLogDto>()))
            .Returns(Task.CompletedTask);

        var service =
            new InvestmentRolloverRequestService(
                requestRepository.Object,
                quoteService.Object,
                accountRepository.Object,
                approvalPolicyService.Object,
                new Mock<IInvestmentPlacementRepository>()
                    .Object,
                new Mock<ITreasuryTransactionRepository>()
                    .Object,
                new Mock<ILedgerRepository>()
                    .Object,
                new Mock<ICashFlowForecastRepository>()
                    .Object,
                currentUserService.Object,
                auditLogService.Object);

        var result =
            await service.Create(
                placementId,
                new CreateInvestmentRolloverRequestDto
                {
                    CashPayoutAccountId =
                        payoutAccountId,

                    IdempotencyKey =
                        "rollover-request-001"
                });

        Assert.NotNull(savedRequest);

        Assert.Equal(
            InvestmentRolloverStatuses.Pending,
            result.Status);

        Assert.Equal(
            2,
            result.RequiredApprovalCount);

        Assert.Equal(
            10_000_000m,
            savedRequest!.RolloverPrincipalAmount);

        Assert.Equal(
            900_000m,
            savedRequest.CashPayoutAmount);

        Assert.Equal(
            payoutAccountId,
            savedRequest.CashPayoutAccountId);
    }

    [Fact]
    public async Task Approve_RequesterIsReviewer_ThrowsForbidden()
    {
        var userId = Guid.NewGuid();

        var request =
            new InvestmentRolloverRequest
            {
                Id = Guid.NewGuid(),

                RequestedByUserId =
                    userId,

                Status =
                    InvestmentRolloverStatuses.Pending,

                ExpiresAtUtc =
                    DateTime.UtcNow.AddHours(1),

                RequiredApprovalCount =
                    1
            };

        var requestRepository =
            new Mock<IInvestmentRolloverRequestRepository>();

        requestRepository
            .Setup(repository =>
                repository.GetById(
                    request.Id))
            .ReturnsAsync(
                request);

        var currentUserService =
            new Mock<ICurrentUserService>();

        currentUserService
            .Setup(service =>
                service.UserId)
            .Returns(
                userId);

        var service =
            new InvestmentRolloverRequestService(
                requestRepository.Object,
                new Mock<IInvestmentRolloverService>()
                    .Object,
                new Mock<IAccountRepository>()
                    .Object,
                new Mock<IApprovalPolicyService>()
                    .Object,
                new Mock<IInvestmentPlacementRepository>()
                    .Object,
                new Mock<ITreasuryTransactionRepository>()
                    .Object,
                new Mock<ILedgerRepository>()
                    .Object,
                new Mock<ICashFlowForecastRepository>()
                    .Object,
                currentUserService.Object,
                new Mock<IAuditLogService>()
                    .Object);

        await Assert.ThrowsAsync<
            ForbiddenOperationException>(
                () => service.Approve(
                    request.Id));
    }
}