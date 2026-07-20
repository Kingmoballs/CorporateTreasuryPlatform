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
    public async Task Execute_PrincipalOnly_CreatesReplacementAndPaysInterest()
    {
        var nowUtc = DateTime.UtcNow.Date;
        var requesterId = Guid.NewGuid();
        var executorId = Guid.NewGuid();

        var sourceAccount =
            new Account
            {
                Id = Guid.NewGuid(),
                Name = "Source Account",
                Currency = "NGN",
                Balance = 20_000_000m,
                IsActive = true,
                ConcurrencyToken = Guid.NewGuid()
            };

        var payoutAccount =
            new Account
            {
                Id = Guid.NewGuid(),
                Name = "Payout Account",
                Currency = "NGN",
                Balance = 5_000_000m,
                IsActive = true,
                ConcurrencyToken = Guid.NewGuid()
            };

        var originalForecast =
            new CashFlowForecastItem
            {
                Id = Guid.NewGuid(),
                AccountId = sourceAccount.Id,
                Amount = 11_000_000m,
                Currency = "NGN",
                Direction = CashFlowDirections.Inflow,
                ExpectedDateUtc = nowUtc,
                Category = "Investment Maturity",
                Description = "Original maturity",
                SourceType =
                    CashFlowForecastSourceTypes.Investment,
                Status = CashFlowForecastStatus.Active,
                ConcurrencyToken = Guid.NewGuid()
            };

        var originalPlacement =
            new InvestmentPlacement
            {
                Id = Guid.NewGuid(),
                Reference = "INV-ORIGINAL-001",
                InvestmentType =
                    InvestmentPlacementTypes.FixedDeposit,
                InstitutionName = "Test Bank",
                SourceAccountId = sourceAccount.Id,
                SourceAccount = sourceAccount,
                PrincipalAmount = 10_000_000m,
                Currency = "NGN",
                AnnualInterestRate = 10m,
                DayCountBasis = 365,
                StartDateUtc = nowUtc.AddDays(-365),
                MaturityDateUtc = nowUtc,
                ExpectedInterestAmount = 1_000_000m,
                ExpectedMaturityAmount = 11_000_000m,
                Status = InvestmentPlacementStatuses.Matured,
                MaturityForecastItemId = originalForecast.Id,
                MaturityForecastItem = originalForecast,
                ConcurrencyToken = Guid.NewGuid()
            };

        var request =
            new InvestmentRolloverRequest
            {
                Id = Guid.NewGuid(),
                OriginalInvestmentPlacementId =
                    originalPlacement.Id,
                OriginalInvestmentPlacement =
                    originalPlacement,
                OriginalInvestmentReference =
                    originalPlacement.Reference,
                OriginalInstitutionName =
                    originalPlacement.InstitutionName,
                Currency = "NGN",
                OriginalMaturityDateUtc = nowUtc,
                OriginalPrincipalAmount = 10_000_000m,
                GrossInterestAmount = 1_000_000m,
                GrossMaturityAmount = 11_000_000m,
                WithholdingTaxRatePercentage = 10m,
                WithholdingTaxAmount = 100_000m,
                NetInterestAmount = 900_000m,
                NetMaturityProceeds = 10_900_000m,
                RolloverOption =
                    InvestmentRolloverOptions.PrincipalOnly,
                RolloverPrincipalAmount = 10_000_000m,
                CashPayoutAmount = 900_000m,
                CashPayoutAccountId = payoutAccount.Id,
                CashPayoutAccount = payoutAccount,
                NewInvestmentType =
                    InvestmentPlacementTypes.FixedDeposit,
                NewInstitutionName = "Test Bank",
                NewAnnualInterestRate = 12m,
                NewDayCountBasis = 365,
                NewStartDateUtc = nowUtc,
                NewMaturityDateUtc =
                    nowUtc.AddDays(365),
                NewTenorDays = 365,
                NewExpectedInterestAmount = 1_200_000m,
                NewExpectedMaturityAmount = 11_200_000m,
                RequestIdempotencyKey =
                    "rollover-request-001",
                ExecutionIdempotencyKey =
                    "INVESTMENT-ROLLOVER-EXECUTION-001",
                Status = InvestmentRolloverStatuses.Approved,
                RequiredApprovalCount = 1,
                ApprovalCount = 1,
                RequestedByUserId = requesterId,
                RequestedAtUtc = nowUtc.AddHours(-2),
                ExpiresAtUtc = nowUtc.AddHours(22),
                ConcurrencyToken = Guid.NewGuid()
            };

        var requestRepository =
            new Mock<IInvestmentRolloverRequestRepository>();

        requestRepository
            .Setup(repository =>
                repository.GetById(request.Id))
            .ReturnsAsync(request);

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

        var placementRepository =
            new Mock<IInvestmentPlacementRepository>();

        placementRepository
            .Setup(repository =>
                repository.ReferenceExists(
                    It.IsAny<string>()))
            .ReturnsAsync(false);

        InvestmentPlacement? newPlacement = null;

        placementRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<InvestmentPlacement>()))
            .Callback<InvestmentPlacement>(
                placement => newPlacement = placement)
            .Returns(Task.CompletedTask);

        var transactionRepository =
            new Mock<ITreasuryTransactionRepository>();

        transactionRepository
            .Setup(repository =>
                repository.GetByIdempotencyKey(
                    request.ExecutionIdempotencyKey))
            .ReturnsAsync(
                (TreasuryTransaction?)null);

        TreasuryTransaction? payoutTransaction = null;

        transactionRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<TreasuryTransaction>()))
            .Callback<TreasuryTransaction>(
                transaction =>
                    payoutTransaction = transaction)
            .Returns(Task.CompletedTask);

        var ledgerRepository =
            new Mock<ILedgerRepository>();

        LedgerEntry? payoutLedger = null;

        ledgerRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<LedgerEntry>()))
            .Callback<LedgerEntry>(
                entry => payoutLedger = entry)
            .Returns(Task.CompletedTask);

        var forecastRepository =
            new Mock<ICashFlowForecastRepository>();

        CashFlowForecastItem? newForecast = null;

        forecastRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<CashFlowForecastItem>()))
            .Callback<CashFlowForecastItem>(
                forecast => newForecast = forecast)
            .Returns(Task.CompletedTask);

        var currentUserService =
            new Mock<ICurrentUserService>();

        currentUserService
            .Setup(service => service.UserId)
            .Returns(executorId);

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
                new Mock<IInvestmentRolloverService>()
                    .Object,
                accountRepository.Object,
                new Mock<IApprovalPolicyService>()
                    .Object,
                placementRepository.Object,
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
            5_900_000m,
            payoutAccount.Balance);

        Assert.NotNull(payoutTransaction);

        Assert.Equal(
            900_000m,
            payoutTransaction!.Amount);

        Assert.NotNull(payoutLedger);

        Assert.Equal(
            900_000m,
            payoutLedger!.Amount);

        Assert.Equal(
            CashFlowForecastStatus.Realized,
            originalForecast.Status);

        Assert.NotNull(newForecast);

        Assert.Equal(
            CashFlowForecastStatus.Active,
            newForecast!.Status);

        Assert.Equal(
            newPlacement.Id,
            request.NewInvestmentPlacementId);
    }
}