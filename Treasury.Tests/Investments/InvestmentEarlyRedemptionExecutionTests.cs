using Moq;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Investments;

public class InvestmentEarlyRedemptionExecutionTests
{
    [Fact]
    public async Task Execute_ApprovedRequest_CreditsAccountOnce()
    {
        var requesterUserId =
            Guid.NewGuid();

        var executorUserId =
            Guid.NewGuid();

        var transactionId =
            Guid.Empty;

        var destinationAccount =
            new Account
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Operations Account",

                Currency =
                    "NGN",

                Balance =
                    1_000_000m,

                IsActive =
                    true,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        var maturityForecast =
            new CashFlowForecastItem
            {
                Id =
                    Guid.NewGuid(),

                Status =
                    CashFlowForecastStatus.Active,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        var placement =
            new InvestmentPlacement
            {
                Id =
                    Guid.NewGuid(),

                Reference =
                    "INV-EARLY-EXEC-001",

                InstitutionName =
                    "Test Bank",

                Currency =
                    "NGN",

                PrincipalAmount =
                    10_000_000m,

                Status =
                    InvestmentPlacementStatuses.Active,

                MaturityDateUtc =
                    DateTime.UtcNow.Date.AddDays(30),

                MaturityForecastItem =
                    maturityForecast,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        var request =
            new InvestmentEarlyRedemptionRequest
            {
                Id =
                    Guid.NewGuid(),

                InvestmentPlacementId =
                    placement.Id,

                InvestmentPlacement =
                    placement,

                InvestmentReference =
                    placement.Reference,

                InstitutionName =
                    placement.InstitutionName,

                DestinationAccountId =
                    destinationAccount.Id,

                DestinationAccount =
                    destinationAccount,

                Currency =
                    "NGN",

                ProposedRedemptionDateUtc =
                    DateTime.UtcNow.Date,

                PrincipalAmount =
                    10_000_000m,

                GrossAccruedInterestAmount =
                    500_000m,

                PenaltyRatePercentage =
                    20m,

                PenaltyAmount =
                    100_000m,

                InterestAfterPenaltyAmount =
                    400_000m,

                WithholdingTaxRatePercentage =
                    10m,

                WithholdingTaxAmount =
                    40_000m,

                NetInterestAmount =
                    360_000m,

                EstimatedRedemptionProceeds =
                    10_360_000m,

                RequestIdempotencyKey =
                    "EARLY-REQUEST-EXEC-001",

                ExecutionIdempotencyKey =
                    $"EARLY-REDEMPTION-" +
                    $"{Guid.NewGuid():N}",

                Status =
                    InvestmentEarlyRedemptionStatuses
                        .Approved,

                RequiredApprovalCount =
                    1,

                ApprovalCount =
                    1,

                RequestedByUserId =
                    requesterUserId,

                RequestedAtUtc =
                    DateTime.UtcNow.AddHours(-1),

                ExpiresAtUtc =
                    DateTime.UtcNow.AddHours(23),

                Decisions =
                    new List<
                        InvestmentEarlyRedemptionDecision>()
            };

        var requestRepository =
            new Mock<
                IInvestmentEarlyRedemptionRequestRepository>();

        requestRepository
            .Setup(repository =>
                repository.GetById(
                    request.Id))
            .ReturnsAsync(
                request);

        var quoteService =
            new Mock<IInvestmentEarlyRedemptionService>();

        var accountRepository =
            new Mock<IAccountRepository>();

        accountRepository
            .Setup(repository =>
                repository.BeginTransaction())
            .Returns(Task.CompletedTask);

        accountRepository
            .Setup(repository =>
                repository.SaveChanges())
            .Returns(Task.CompletedTask);

        accountRepository
            .Setup(repository =>
                repository.CommitTransaction())
            .Returns(Task.CompletedTask);

        accountRepository
            .Setup(repository =>
                repository.RollbackTransaction())
            .Returns(Task.CompletedTask);

        var approvalPolicyService =
            new Mock<IApprovalPolicyService>();

        var transactionRepository =
            new Mock<ITreasuryTransactionRepository>();

        transactionRepository
            .Setup(repository =>
                repository.GetByIdempotencyKey(
                    request.ExecutionIdempotencyKey))
            .ReturnsAsync(
                (TreasuryTransaction?)null);

        transactionRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<TreasuryTransaction>()))
            .Callback<TreasuryTransaction>(
                transaction =>
                    transactionId =
                        transaction.Id)
            .Returns(Task.CompletedTask);

        var ledgerRepository =
            new Mock<ILedgerRepository>();

        ledgerRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<LedgerEntry>()))
            .Returns(Task.CompletedTask);

        var currentUserService =
            new Mock<ICurrentUserService>();

        currentUserService
            .Setup(service =>
                service.UserId)
            .Returns(
                executorUserId);

        var auditLogService =
            new Mock<IAuditLogService>();

        auditLogService
            .Setup(service =>
                service.Record(
                    It.IsAny<CreateAuditLogDto>()))
            .Returns(Task.CompletedTask);

        var service =
            new InvestmentEarlyRedemptionRequestService(
                requestRepository.Object,
                quoteService.Object,
                accountRepository.Object,
                approvalPolicyService.Object,
                transactionRepository.Object,
                ledgerRepository.Object,
                currentUserService.Object,
                auditLogService.Object);

        var result =
            await service.Execute(
                request.Id);

        Assert.Equal(
            InvestmentEarlyRedemptionStatuses.Executed,
            result.Status);

        Assert.Equal(
            11_360_000m,
            destinationAccount.Balance);

        Assert.Equal(
            InvestmentPlacementStatuses.Redeemed,
            placement.Status);

        Assert.Equal(
            10_360_000m,
            placement.ActualMaturityAmount);

        Assert.Equal(
            CashFlowForecastStatus.Realized,
            maturityForecast.Status);

        Assert.Equal(
            transactionId,
            request.RedemptionTreasuryTransactionId);

        accountRepository.Verify(
            repository =>
                repository.CommitTransaction(),
            Times.Once);

        accountRepository.Verify(
            repository =>
                repository.RollbackTransaction(),
            Times.Never);
    }
}