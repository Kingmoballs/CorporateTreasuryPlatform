using Moq;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Investments;

public class InvestmentRolloverExecutionTests
{
    [Fact]
    public async Task
        Execute_PrincipalOnly_CreatesReplacementAndPaysInterest()
    {
        var nowUtc = DateTime.UtcNow;
        var todayUtc = nowUtc.Date;

        var requesterId = Guid.NewGuid();
        var executorId = Guid.NewGuid();

        var counterparty =
            new Counterparty
            {
                Id =
                    Guid.NewGuid(),

                Code =
                    "TESTBANK",

                Name =
                    "Test Bank",

                CounterpartyType =
                    CounterpartyTypes.Bank,

                CountryCode =
                    "NG",

                IsActive =
                    true,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        var sourceAccount =
            new Account
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Source Account",

                Currency =
                    "NGN",

                Balance =
                    20_000_000m,

                IsActive =
                    true,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        var payoutAccount =
            new Account
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Payout Account",

                Currency =
                    "NGN",

                Balance =
                    5_000_000m,

                IsActive =
                    true,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        var originalForecast =
            new CashFlowForecastItem
            {
                Id =
                    Guid.NewGuid(),

                AccountId =
                    sourceAccount.Id,

                Account =
                    sourceAccount,

                Amount =
                    11_000_000m,

                Currency =
                    "NGN",

                Direction =
                    CashFlowDirections.Inflow,

                ExpectedDateUtc =
                    todayUtc,

                Category =
                    "Investment Maturity",

                CounterpartyName =
                    counterparty.Name,

                Description =
                    "Original maturity",

                SourceType =
                    CashFlowForecastSourceTypes
                        .Investment,

                Status =
                    CashFlowForecastStatus.Active,

                CreatedAtUtc =
                    nowUtc.AddDays(-365),

                UpdatedAtUtc =
                    nowUtc,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        var originalPlacement =
            new InvestmentPlacement
            {
                Id =
                    Guid.NewGuid(),

                Reference =
                    "INV-ORIGINAL-001",

                InvestmentType =
                    InvestmentPlacementTypes
                        .FixedDeposit,

                InstitutionName =
                    counterparty.Name,

                CounterpartyId =
                    counterparty.Id,

                Counterparty =
                    counterparty,

                SourceAccountId =
                    sourceAccount.Id,

                SourceAccount =
                    sourceAccount,

                PrincipalAmount =
                    10_000_000m,

                Currency =
                    "NGN",

                AnnualInterestRate =
                    10m,

                DayCountBasis =
                    365,

                StartDateUtc =
                    todayUtc.AddDays(-365),

                MaturityDateUtc =
                    todayUtc,

                ExpectedInterestAmount =
                    1_000_000m,

                ExpectedMaturityAmount =
                    11_000_000m,

                Status =
                    InvestmentPlacementStatuses.Matured,

                MaturityForecastItemId =
                    originalForecast.Id,

                MaturityForecastItem =
                    originalForecast,

                CreatedAtUtc =
                    nowUtc.AddDays(-365),

                UpdatedAtUtc =
                    nowUtc,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        var request =
            new InvestmentRolloverRequest
            {
                Id =
                    Guid.NewGuid(),

                OriginalInvestmentPlacementId =
                    originalPlacement.Id,

                OriginalInvestmentPlacement =
                    originalPlacement,

                OriginalInvestmentReference =
                    originalPlacement.Reference,

                OriginalInstitutionName =
                    originalPlacement.InstitutionName,

                Currency =
                    "NGN",

                OriginalMaturityDateUtc =
                    todayUtc,

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

                CashPayoutAccountId =
                    payoutAccount.Id,

                CashPayoutAccount =
                    payoutAccount,

                NewInvestmentType =
                    InvestmentPlacementTypes
                        .FixedDeposit,

                /*
                 * Rollovers remain with the original
                 * counterparty.
                 */
                NewInstitutionName =
                    counterparty.Name,

                NewAnnualInterestRate =
                    12m,

                NewDayCountBasis =
                    365,

                NewStartDateUtc =
                    todayUtc,

                NewMaturityDateUtc =
                    todayUtc.AddDays(365),

                NewTenorDays =
                    365,

                NewExpectedInterestAmount =
                    1_200_000m,

                NewExpectedMaturityAmount =
                    11_200_000m,

                RequestIdempotencyKey =
                    "rollover-request-001",

                ExecutionIdempotencyKey =
                    "INVESTMENT-ROLLOVER-EXECUTION-001",

                Status =
                    InvestmentRolloverStatuses.Approved,

                RequiredApprovalCount =
                    1,

                ApprovalCount =
                    1,

                RequestedByUserId =
                    requesterId,

                RequestedAtUtc =
                    nowUtc.AddHours(-2),

                ExpiresAtUtc =
                    nowUtc.AddHours(22),

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        var requestRepository =
            new Mock<
                IInvestmentRolloverRequestRepository>();

        requestRepository
            .Setup(repository =>
                repository.GetById(request.Id))
            .ReturnsAsync(request);

        var quoteService =
            new Mock<IInvestmentRolloverService>();

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

        var placementRepository =
            new Mock<IInvestmentPlacementRepository>();

        placementRepository
            .Setup(repository =>
                repository.ReferenceExists(
                    It.IsAny<string>()))
            .ReturnsAsync(false);

        InvestmentPlacement? newPlacement =
            null;

        placementRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<InvestmentPlacement>()))
            .Callback<InvestmentPlacement>(
                placement =>
                    newPlacement = placement)
            .Returns(Task.CompletedTask);

        var limitEnforcementService =
            new Mock<
                IInvestmentLimitEnforcementService>();

        limitEnforcementService
            .Setup(service =>
                service.EnsureWithinLimits(
                    counterparty.Id,
                    "NGN",
                    InvestmentPlacementTypes
                        .FixedDeposit,
                    request.RolloverPrincipalAmount,
                    originalPlacement.Id))
            .Returns(Task.CompletedTask);

        var transactionRepository =
            new Mock<
                ITreasuryTransactionRepository>();

        transactionRepository
            .Setup(repository =>
                repository.GetByIdempotencyKey(
                    request.ExecutionIdempotencyKey))
            .ReturnsAsync(
                (TreasuryTransaction?)null);

        TreasuryTransaction? payoutTransaction =
            null;

        transactionRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<TreasuryTransaction>()))
            .Callback<TreasuryTransaction>(
                transaction =>
                    payoutTransaction =
                        transaction)
            .Returns(Task.CompletedTask);

        var ledgerRepository =
            new Mock<ILedgerRepository>();

        LedgerEntry? payoutLedger =
            null;

        ledgerRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<LedgerEntry>()))
            .Callback<LedgerEntry>(
                entry =>
                    payoutLedger = entry)
            .Returns(Task.CompletedTask);

        var forecastRepository =
            new Mock<ICashFlowForecastRepository>();

        CashFlowForecastItem? newForecast =
            null;

        forecastRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<CashFlowForecastItem>()))
            .Callback<CashFlowForecastItem>(
                forecast =>
                    newForecast = forecast)
            .Returns(Task.CompletedTask);

        var currentUserService =
            new Mock<ICurrentUserService>();

        currentUserService
            .Setup(service =>
                service.UserId)
            .Returns(executorId);

        var auditLogService =
            new Mock<IAuditLogService>();

        auditLogService
            .Setup(service =>
                service.Record(
                    It.IsAny<CreateAuditLogDto>()))
            .Returns(Task.CompletedTask);

        /*
         * The constructor order must exactly match:
         *
         * request repository
         * quote service
         * account repository
         * approval-policy service
         * placement repository
         * limit-enforcement service
         * transaction repository
         * ledger repository
         * forecast repository
         * current-user service
         * audit service
         */
        var service =
            new InvestmentRolloverRequestService(
                requestRepository.Object,
                quoteService.Object,
                accountRepository.Object,
                approvalPolicyService.Object,
                placementRepository.Object,
                limitEnforcementService.Object,
                transactionRepository.Object,
                ledgerRepository.Object,
                forecastRepository.Object,
                currentUserService.Object,
                auditLogService.Object);

        var result =
            await service.Execute(request.Id);

        Assert.Equal(
            InvestmentRolloverStatuses.Executed,
            result.Status);

        Assert.Equal(
            InvestmentPlacementStatuses.Redeemed,
            originalPlacement.Status);

        Assert.NotNull(newPlacement);

        Assert.Equal(
            InvestmentPlacementStatuses.Active,
            newPlacement!.Status);

        Assert.Equal(
            10_000_000m,
            newPlacement.PrincipalAmount);

        Assert.Equal(
            counterparty.Id,
            newPlacement.CounterpartyId);

        Assert.Same(
            counterparty,
            newPlacement.Counterparty);

        Assert.Equal(
            counterparty.Name,
            newPlacement.InstitutionName);

        Assert.Equal(
            5_900_000m,
            payoutAccount.Balance);

        Assert.NotNull(payoutTransaction);

        Assert.Equal(
            900_000m,
            payoutTransaction!.Amount);

        Assert.Equal(
            request.ExecutionIdempotencyKey,
            payoutTransaction.IdempotencyKey);

        Assert.NotNull(payoutLedger);

        Assert.Equal(
            900_000m,
            payoutLedger!.Amount);

        Assert.Equal(
            "Debit",
            payoutLedger.EntryType);

        Assert.Equal(
            CashFlowForecastStatus.Realized,
            originalForecast.Status);

        Assert.Equal(
            payoutTransaction.Id,
            originalForecast
                .RealizedTreasuryTransactionId);

        Assert.NotNull(newForecast);

        Assert.Equal(
            CashFlowForecastStatus.Active,
            newForecast!.Status);

        Assert.Equal(
            newPlacement.Id,
            request.NewInvestmentPlacementId);

        Assert.Equal(
            payoutTransaction.Id,
            request.CashPayoutTreasuryTransactionId);

        Assert.Equal(
            executorId,
            request.ExecutedByUserId);

        limitEnforcementService.Verify(
            serviceMock =>
                serviceMock.EnsureWithinLimits(
                    counterparty.Id,
                    "NGN",
                    InvestmentPlacementTypes
                        .FixedDeposit,
                    10_000_000m,
                    originalPlacement.Id),
            Times.Once);

        placementRepository.Verify(
            repository =>
                repository.Add(
                    It.IsAny<InvestmentPlacement>()),
            Times.Once);

        transactionRepository.Verify(
            repository =>
                repository.Add(
                    It.IsAny<TreasuryTransaction>()),
            Times.Once);

        ledgerRepository.Verify(
            repository =>
                repository.Add(
                    It.IsAny<LedgerEntry>()),
            Times.Once);

        forecastRepository.Verify(
            repository =>
                repository.Add(
                    It.IsAny<CashFlowForecastItem>()),
            Times.Once);

        accountRepository.Verify(
            repository =>
                repository.SaveChanges(),
            Times.Once);

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