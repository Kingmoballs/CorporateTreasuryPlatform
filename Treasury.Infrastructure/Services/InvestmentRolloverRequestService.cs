using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;
using Treasury.Shared.Common;

namespace Treasury.Infrastructure.Services;

public class InvestmentRolloverRequestService
    : IInvestmentRolloverRequestService
{
    private readonly IInvestmentRolloverRequestRepository
        _requestRepository;

    private readonly IInvestmentRolloverService
        _quoteService;

    private readonly IAccountRepository
        _accountRepository;

    private readonly IApprovalPolicyService
        _approvalPolicyService;

    private readonly IInvestmentPlacementRepository
        _placementRepository;

    private readonly ITreasuryTransactionRepository
        _transactionRepository;

    private readonly ILedgerRepository
        _ledgerRepository;

    private readonly ICashFlowForecastRepository
        _forecastRepository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuditLogService
        _auditLogService;

    public InvestmentRolloverRequestService(
        IInvestmentRolloverRequestRepository requestRepository,
        IInvestmentRolloverService quoteService,
        IAccountRepository accountRepository,
        IApprovalPolicyService approvalPolicyService,
        IInvestmentPlacementRepository placementRepository,
        ITreasuryTransactionRepository transactionRepository,
        ILedgerRepository ledgerRepository,
        ICashFlowForecastRepository forecastRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _requestRepository = requestRepository;
        _quoteService = quoteService;
        _accountRepository = accountRepository;
        _approvalPolicyService = approvalPolicyService;
        _placementRepository = placementRepository;
        _transactionRepository = transactionRepository;
        _ledgerRepository = ledgerRepository;
        _forecastRepository = forecastRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<InvestmentRolloverRequestResponseDto>
        Create(
            Guid investmentPlacementId,
            CreateInvestmentRolloverRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (investmentPlacementId == Guid.Empty)
        {
            throw new RequestValidationException(
                "A valid investment placement ID is required.");
        }

        var idempotencyKey =
            NormalizeRequiredText(
                dto.IdempotencyKey,
                "Idempotency key",
                100);

        var existingRequest =
            await _requestRepository
                .GetByIdempotencyKey(
                    idempotencyKey);

        if (existingRequest is not null)
        {
            if (existingRequest
                    .OriginalInvestmentPlacementId !=
                investmentPlacementId)
            {
                throw new ConflictException(
                    "The idempotency key has already been " +
                    "used for another investment rollover.");
            }

            return Map(existingRequest);
        }

        await EnsureNoOpenRequest(
            investmentPlacementId);

        var quote =
            await _quoteService.GetQuote(
                investmentPlacementId,
                dto);

        var payoutAccount =
            await GetPayoutAccount(
                dto.CashPayoutAccountId,
                quote);

        var approvalRequirements =
            await _approvalPolicyService.GetRequirements(
                ApprovalOperationTypes.InvestmentRollover,
                quote.Currency);

        var nowUtc = DateTime.UtcNow;
        var requestId = Guid.NewGuid();

        var request =
            new InvestmentRolloverRequest
            {
                Id = requestId,

                OriginalInvestmentPlacementId =
                    quote.OriginalInvestmentPlacementId,

                OriginalInvestmentReference =
                    quote.OriginalInvestmentReference,

                OriginalInstitutionName =
                    quote.OriginalInstitutionName,

                Currency =
                    quote.Currency,

                OriginalMaturityDateUtc =
                    quote.OriginalMaturityDateUtc,

                OriginalPrincipalAmount =
                    quote.OriginalPrincipalAmount,

                GrossInterestAmount =
                    quote.GrossInterestAmount,

                GrossMaturityAmount =
                    quote.GrossMaturityAmount,

                WithholdingTaxRatePercentage =
                    quote.WithholdingTaxRatePercentage,

                WithholdingTaxAmount =
                    quote.WithholdingTaxAmount,

                NetInterestAmount =
                    quote.NetInterestAmount,

                NetMaturityProceeds =
                    quote.NetMaturityProceeds,

                RolloverOption =
                    quote.RolloverOption,

                RolloverPrincipalAmount =
                    quote.RolloverPrincipalAmount,

                CashPayoutAmount =
                    quote.CashPayoutAmount,

                CashPayoutAccountId =
                    payoutAccount?.Id,

                CashPayoutAccount =
                    payoutAccount,

                NewInvestmentType =
                    quote.NewInvestmentType,

                NewInstitutionName =
                    quote.NewInstitutionName,

                NewAnnualInterestRate =
                    quote.NewAnnualInterestRate,

                NewDayCountBasis =
                    quote.NewDayCountBasis,

                NewStartDateUtc =
                    quote.NewStartDateUtc,

                NewMaturityDateUtc =
                    quote.NewMaturityDateUtc,

                NewTenorDays =
                    quote.NewTenorDays,

                NewExpectedInterestAmount =
                    quote.NewExpectedInterestAmount,

                NewExpectedMaturityAmount =
                    quote.NewExpectedMaturityAmount,

                RequestIdempotencyKey =
                    idempotencyKey,

                ExecutionIdempotencyKey =
                    $"INVESTMENT-ROLLOVER-{requestId:N}",

                ExternalReference =
                    NormalizeOptionalText(
                        dto.ExternalReference,
                        100),

                Notes =
                    NormalizeOptionalText(
                        dto.Notes,
                        1000),

                Status =
                    InvestmentRolloverStatuses.Pending,

                RequiredApprovalCount =
                    approvalRequirements
                        .RequiredApprovalCount,

                ApprovalCount = 0,

                RequestedByUserId =
                    _currentUserService.UserId,

                RequestedAtUtc =
                    nowUtc,

                ExpiresAtUtc =
                    nowUtc.AddHours(
                        approvalRequirements
                            .PendingRequestExpiryHours),

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        try
        {
            await _requestRepository.Add(request);
            await _requestRepository.SaveChanges();
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The rollover request could not be saved. " +
                "An open or duplicate request may already exist.");
        }

        await RecordAudit(
            request,
            AuditActionTypes.Created,
            $"Investment rollover request {request.Id} " +
            $"was created for investment " +
            $"{request.OriginalInvestmentReference}.");

        return Map(request);
    }

    public async Task<InvestmentRolloverRequestResponseDto>
        GetById(Guid id)
    {
        var request =
            await GetRequest(id);

        return Map(request);
    }

    public async Task<List<
        InvestmentRolloverRequestResponseDto>>
        GetPending()
    {
        var requests =
            await _requestRepository.GetPending();

        return requests
            .Select(Map)
            .ToList();
    }

    public async Task<InvestmentRolloverRequestResponseDto>
        Approve(Guid id)
    {
        var request =
            await GetRequest(id);

        await EnsurePending(request);

        var currentUserId =
            _currentUserService.UserId;

        EnsureDifferentReviewer(
            request,
            currentUserId);

        if (await _requestRepository.HasDecision(
            request.Id,
            currentUserId))
        {
            throw new ConflictException(
                "You have already reviewed this " +
                "rollover request.");
        }

        var decision =
            new InvestmentRolloverDecision
            {
                Id = Guid.NewGuid(),

                InvestmentRolloverRequestId =
                    request.Id,

                InvestmentRolloverRequest =
                    request,

                ApproverUserId =
                    currentUserId,

                Decision =
                    ApprovalDecisionTypes.Approved,

                CreatedAtUtc =
                    DateTime.UtcNow
            };

        request.Decisions.Add(decision);

        await _requestRepository.AddDecision(
            decision);

        request.ApprovalCount += 1;

        if (request.ApprovalCount >=
            request.RequiredApprovalCount)
        {
            request.Status =
                InvestmentRolloverStatuses.Approved;
        }

        request.ConcurrencyToken =
            Guid.NewGuid();

        await SaveReviewChanges();

        await RecordAudit(
            request,
            AuditActionTypes.Approved,
            request.Status ==
                InvestmentRolloverStatuses.Approved
                ? $"Rollover request {request.Id} " +
                  "received final approval."
                : $"Rollover request {request.Id} " +
                  "received partial approval.");

        return Map(request);
    }

    public async Task<InvestmentRolloverRequestResponseDto>
        Reject(
            Guid id,
            string reason)
    {
        var request =
            await GetRequest(id);

        await EnsurePending(request);

        var currentUserId =
            _currentUserService.UserId;

        EnsureDifferentReviewer(
            request,
            currentUserId);

        if (await _requestRepository.HasDecision(
            request.Id,
            currentUserId))
        {
            throw new ConflictException(
                "You have already reviewed this " +
                "rollover request.");
        }

        var normalizedReason =
            NormalizeRequiredText(
                reason,
                "Rejection reason",
                500);

        var rejectedAtUtc =
            DateTime.UtcNow;

        var decision =
            new InvestmentRolloverDecision
            {
                Id = Guid.NewGuid(),

                InvestmentRolloverRequestId =
                    request.Id,

                InvestmentRolloverRequest =
                    request,

                ApproverUserId =
                    currentUserId,

                Decision =
                    ApprovalDecisionTypes.Rejected,

                Comment =
                    normalizedReason,

                CreatedAtUtc =
                    rejectedAtUtc
            };

        request.Decisions.Add(decision);

        await _requestRepository.AddDecision(
            decision);

        request.Status =
            InvestmentRolloverStatuses.Rejected;

        request.RejectedByUserId =
            currentUserId;

        request.RejectedAtUtc =
            rejectedAtUtc;

        request.RejectionReason =
            normalizedReason;

        request.ConcurrencyToken =
            Guid.NewGuid();

        await SaveReviewChanges();

        await RecordAudit(
            request,
            AuditActionTypes.Rejected,
            $"Rollover request {request.Id} " +
            "was rejected.");

        return Map(request);
    }

    public async Task<InvestmentRolloverRequestResponseDto>
        Execute(Guid id)
    {
        await _accountRepository.BeginTransaction();

        try
        {
            var request =
                await GetRequest(id);

            /*
            * A retry after a successful commit returns the
            * existing result without executing twice.
            */
            if (request.Status ==
                InvestmentRolloverStatuses.Executed)
            {
                await _accountRepository.CommitTransaction();

                return Map(request);
            }

            if (request.Status !=
                InvestmentRolloverStatuses.Approved)
            {
                throw new ConflictException(
                    "Only an approved rollover request " +
                    "can be executed.");
            }

            if (request.ApprovalCount <
                request.RequiredApprovalCount)
            {
                throw new ConflictException(
                    "The rollover request has not received " +
                    "all required approvals.");
            }

            var executedByUserId =
                _currentUserService.UserId;

            if (request.RequestedByUserId ==
                executedByUserId)
            {
                throw new ForbiddenOperationException(
                    "The requester cannot execute their own " +
                    "rollover request.");
            }

            var nowUtc =
                DateTime.UtcNow;

            var originalPlacement =
                request.OriginalInvestmentPlacement;

            if (originalPlacement is null)
            {
                throw new ConflictException(
                    "The original investment placement " +
                    "was not loaded.");
            }

            /*
            * Permit execution even when the maturity worker
            * has not yet changed Active to Matured.
            */
            if (originalPlacement.Status ==
                    InvestmentPlacementStatuses.Active &&
                originalPlacement.MaturityDateUtc <=
                    nowUtc)
            {
                originalPlacement.Status =
                    InvestmentPlacementStatuses.Matured;
            }

            if (originalPlacement.Status !=
                InvestmentPlacementStatuses.Matured)
            {
                throw new ConflictException(
                    "Only a matured investment placement " +
                    "can be rolled over.");
            }

            if (originalPlacement.MaturityDateUtc.Date >
                nowUtc.Date)
            {
                throw new ConflictException(
                    "The original investment maturity date " +
                    "has not been reached.");
            }

            ValidateOriginalPlacement(
                request,
                originalPlacement);

            ValidateLockedAmounts(request);

            if (request.NewStartDateUtc.Date >
                nowUtc.Date)
            {
                throw new ConflictException(
                    "The replacement investment start date " +
                    "has not been reached.");
            }

            if (request.NewMaturityDateUtc.Date <=
                nowUtc.Date)
            {
                throw new ConflictException(
                    "The replacement investment maturity " +
                    "date must still be in the future.");
            }

            var originalForecast =
                originalPlacement.MaturityForecastItem;

            if (originalForecast is null)
            {
                throw new ConflictException(
                    "The original maturity forecast was " +
                    "not loaded.");
            }

            if (originalForecast.Status !=
                CashFlowForecastStatus.Active)
            {
                throw new ConflictException(
                    "The original maturity forecast has " +
                    "already been processed.");
            }

            var sourceAccount =
                originalPlacement.SourceAccount;

            if (sourceAccount is null)
            {
                throw new ConflictException(
                    "The original investment source account " +
                    "was not loaded.");
            }

            if (!sourceAccount.IsActive)
            {
                throw new ConflictException(
                    "The original investment source account " +
                    "is inactive.");
            }

            if (!string.Equals(
                sourceAccount.Currency,
                request.Currency,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException(
                    "The source account currency must match " +
                    "the rollover currency.");
            }

            var payoutAccount =
                request.CashPayoutAccount;

            ValidateExecutionPayoutAccount(
                request,
                payoutAccount);

            var existingTransaction =
                await _transactionRepository
                    .GetByIdempotencyKey(
                        request.ExecutionIdempotencyKey);

            if (existingTransaction is not null)
            {
                throw new ConflictException(
                    "A transaction already exists for this " +
                    "rollover execution key.");
            }

            var newInvestmentReference =
                await GenerateRolloverReference();

            var newForecast =
                new CashFlowForecastItem
                {
                    Id = Guid.NewGuid(),

                    AccountId =
                        sourceAccount.Id,

                    Account =
                        sourceAccount,

                    Direction =
                        CashFlowDirections.Inflow,

                    Amount =
                        request.NewExpectedMaturityAmount,

                    Currency =
                        request.Currency,

                    ExpectedDateUtc =
                        request.NewMaturityDateUtc,

                    Category =
                        "Investment Maturity",

                    CounterpartyName =
                        request.NewInstitutionName,

                    Description =
                        $"Expected maturity proceeds for " +
                        $"{newInvestmentReference}.",

                    SourceType =
                        CashFlowForecastSourceTypes.Investment,

                    Status =
                        CashFlowForecastStatus.Active,

                    CreatedByUserId =
                        request.RequestedByUserId,

                    CreatedAtUtc =
                        nowUtc,

                    UpdatedAtUtc =
                        nowUtc,

                    ConcurrencyToken =
                        Guid.NewGuid()
                };

            /*
            * The replacement is already approved through the
            * rollover request. It therefore becomes Active
            * without a second activation workflow.
            *
            * No bank balance is debited because the proceeds
            * remained with the investment institution.
            */
            var newPlacement =
                new InvestmentPlacement
                {
                    Id = Guid.NewGuid(),

                    Reference =
                        newInvestmentReference,

                    InvestmentType =
                        request.NewInvestmentType,

                    InstitutionName =
                        request.NewInstitutionName,

                    SourceAccountId =
                        sourceAccount.Id,

                    SourceAccount =
                        sourceAccount,

                    PrincipalAmount =
                        request.RolloverPrincipalAmount,

                    Currency =
                        request.Currency,

                    AnnualInterestRate =
                        request.NewAnnualInterestRate,

                    DayCountBasis =
                        request.NewDayCountBasis,

                    StartDateUtc =
                        request.NewStartDateUtc,

                    MaturityDateUtc =
                        request.NewMaturityDateUtc,

                    ExpectedInterestAmount =
                        request.NewExpectedInterestAmount,

                    ExpectedMaturityAmount =
                        request.NewExpectedMaturityAmount,

                    Status =
                        InvestmentPlacementStatuses.Active,

                    ExternalReference =
                        request.ExternalReference,

                    Notes =
                        request.Notes,

                    CreatedByUserId =
                        request.RequestedByUserId,

                    CreatedAtUtc =
                        nowUtc,

                    UpdatedAtUtc =
                        nowUtc,

                    RequiredApprovalCount =
                        0,

                    ApprovalCount =
                        0,

                    ActivationIdempotencyKey =
                        request.ExecutionIdempotencyKey,

                    MaturityForecastItemId =
                        newForecast.Id,

                    MaturityForecastItem =
                        newForecast,

                    ActivatedByUserId =
                        executedByUserId,

                    ActivatedAtUtc =
                        nowUtc,

                    ConcurrencyToken =
                        Guid.NewGuid()
                };

            TreasuryTransaction? payoutTransaction =
                null;

            LedgerEntry? payoutLedgerEntry =
                null;

            if (request.CashPayoutAmount > 0)
            {
                var description =
                    $"Net-interest payout from rollover of " +
                    $"{originalPlacement.Reference}.";

                payoutTransaction =
                    new TreasuryTransaction
                    {
                        Id =
                            Guid.NewGuid(),

                        Reference =
                            TransactionReferenceGenerator
                                .Generate(),

                        TransactionType =
                            TransactionTypes
                                .InvestmentRedemption,

                        Status =
                            TransactionStatuses.Completed,

                        Amount =
                            request.CashPayoutAmount,

                        Currency =
                            request.Currency,

                        Description =
                            description,

                        SourceAccountId =
                            null,

                        DestinationAccountId =
                            payoutAccount!.Id,

                        Category =
                            "Investment Rollover Interest Payout",

                        CounterpartyName =
                            request.OriginalInstitutionName,

                        ExternalReference =
                            request.ExternalReference ??
                            request.OriginalInvestmentReference,

                        IdempotencyKey =
                            request.ExecutionIdempotencyKey,

                        InitiatedByUserId =
                            request.RequestedByUserId,

                        CompletedByUserId =
                            executedByUserId,

                        CreatedAtUtc =
                            nowUtc,

                        CompletedAtUtc =
                            nowUtc
                    };

                /*
                * Only the cash payout reaches a bank account.
                * The reinvested principal never enters the
                * bank-account ledger.
                */
                payoutAccount.Balance +=
                    request.CashPayoutAmount;

                payoutAccount.ConcurrencyToken =
                    Guid.NewGuid();

                payoutLedgerEntry =
                    new LedgerEntry
                    {
                        Id =
                            Guid.NewGuid(),

                        AccountId =
                            payoutAccount.Id,

                        Account =
                            payoutAccount,

                        TreasuryTransactionId =
                            payoutTransaction.Id,

                        TreasuryTransaction =
                            payoutTransaction,

                        Amount =
                            request.CashPayoutAmount,

                        EntryType =
                            "Debit",

                        Description =
                            description,

                        CreatedAt =
                            nowUtc
                    };

                /*
                * A PrincipalOnly rollover creates a partial
                * cash realization: the net interest payout.
                */
                originalForecast.Status =
                    CashFlowForecastStatus.Realized;

                originalForecast
                    .RealizedTreasuryTransactionId =
                        payoutTransaction.Id;

                originalForecast
                    .RealizedTreasuryTransaction =
                        payoutTransaction;

                originalForecast.RealizedAtUtc =
                    nowUtc;
            }
            else
            {
                /*
                * No original maturity proceeds entered a bank
                * account because everything was reinvested.
                */
                originalForecast.Status =
                    CashFlowForecastStatus.Cancelled;

                originalForecast.CancelledByUserId =
                    executedByUserId;

                originalForecast.CancelledAtUtc =
                    nowUtc;
            }

            originalForecast.UpdatedAtUtc =
                nowUtc;

            originalForecast.ConcurrencyToken =
                Guid.NewGuid();

            var originalBeforeValues =
                new
                {
                    originalPlacement.Id,
                    originalPlacement.Reference,
                    originalPlacement.Status,
                    originalPlacement.PrincipalAmount,
                    originalPlacement.ActualInterestAmount,
                    originalPlacement.WithholdingTaxAmount,
                    originalPlacement.ActualMaturityAmount,
                    originalPlacement.RedemptionAccountId,
                    originalPlacement
                        .RedemptionTreasuryTransactionId,
                    originalPlacement.RedeemedAtUtc
                };

            originalPlacement.Status =
                InvestmentPlacementStatuses.Redeemed;

            originalPlacement.RedemptionIdempotencyKey =
                request.ExecutionIdempotencyKey;

            originalPlacement.RedemptionAccountId =
                payoutAccount?.Id;

            originalPlacement.RedemptionAccount =
                payoutAccount;

            originalPlacement.RedemptionTreasuryTransactionId =
                payoutTransaction?.Id;

            originalPlacement.RedemptionTreasuryTransaction =
                payoutTransaction;

            originalPlacement.ActualInterestAmount =
                request.GrossInterestAmount;

            originalPlacement.WithholdingTaxAmount =
                request.WithholdingTaxAmount;

            originalPlacement.ActualMaturityAmount =
                request.NetMaturityProceeds;

            originalPlacement.RedemptionExternalReference =
                request.ExternalReference;

            originalPlacement.RedemptionNotes =
                request.Notes;

            originalPlacement.RedeemedByUserId =
                executedByUserId;

            originalPlacement.RedeemedAtUtc =
                nowUtc;

            originalPlacement.UpdatedAtUtc =
                nowUtc;

            originalPlacement.ConcurrencyToken =
                Guid.NewGuid();

            request.Status =
                InvestmentRolloverStatuses.Executed;

            request.NewInvestmentPlacementId =
                newPlacement.Id;

            request.NewInvestmentPlacement =
                newPlacement;

            request.CashPayoutTreasuryTransactionId =
                payoutTransaction?.Id;

            request.CashPayoutTreasuryTransaction =
                payoutTransaction;

            request.ExecutedByUserId =
                executedByUserId;

            request.ExecutedAtUtc =
                nowUtc;

            request.ConcurrencyToken =
                Guid.NewGuid();

            await _placementRepository.Add(
                newPlacement);

            await _forecastRepository.Add(
                newForecast);

            if (payoutTransaction is not null)
            {
                await _transactionRepository.Add(
                    payoutTransaction);
            }

            if (payoutLedgerEntry is not null)
            {
                await _ledgerRepository.Add(
                    payoutLedgerEntry);
            }

            /*
            * All loaded entities use the same scoped DbContext.
            * One SaveChanges call persists the entire rollover.
            */
            await _accountRepository.SaveChanges();

            await _auditLogService.Record(
                new CreateAuditLogDto
                {
                    Action =
                        AuditActionTypes.Redeemed,

                    EntityType =
                        AuditEntityTypes.InvestmentPlacement,

                    EntityId =
                        originalPlacement.Id,

                    EntityReference =
                        originalPlacement.Reference,

                    Summary =
                        $"Investment placement " +
                        $"{originalPlacement.Reference} was " +
                        $"rolled over into " +
                        $"{newPlacement.Reference}.",

                    BeforeValues =
                        originalBeforeValues,

                    AfterValues =
                        new
                        {
                            originalPlacement.Id,
                            originalPlacement.Reference,
                            originalPlacement.Status,
                            originalPlacement
                                .ActualInterestAmount,
                            originalPlacement
                                .WithholdingTaxAmount,
                            originalPlacement
                                .ActualMaturityAmount,
                            originalPlacement
                                .RedemptionAccountId,
                            originalPlacement
                                .RedemptionTreasuryTransactionId,
                            originalPlacement.RedeemedByUserId,
                            originalPlacement.RedeemedAtUtc
                        },

                    Metadata =
                        new
                        {
                            Module =
                                "Investment Rollover",

                            RolloverRequestId =
                                request.Id,

                            NewInvestmentPlacementId =
                                newPlacement.Id,

                            NewInvestmentReference =
                                newPlacement.Reference,

                            request.RolloverOption,
                            request.RolloverPrincipalAmount,
                            request.CashPayoutAmount
                        }
                });

            await _auditLogService.Record(
                new CreateAuditLogDto
                {
                    Action =
                        AuditActionTypes.Activated,

                    EntityType =
                        AuditEntityTypes.InvestmentPlacement,

                    EntityId =
                        newPlacement.Id,

                    EntityReference =
                        newPlacement.Reference,

                    Summary =
                        $"Replacement investment " +
                        $"{newPlacement.Reference} was activated " +
                        $"from rollover request {request.Id}.",

                    AfterValues =
                        new
                        {
                            newPlacement.Id,
                            newPlacement.Reference,
                            newPlacement.InvestmentType,
                            newPlacement.InstitutionName,
                            newPlacement.SourceAccountId,
                            newPlacement.PrincipalAmount,
                            newPlacement.Currency,
                            newPlacement.AnnualInterestRate,
                            newPlacement.DayCountBasis,
                            newPlacement.StartDateUtc,
                            newPlacement.MaturityDateUtc,
                            newPlacement.ExpectedInterestAmount,
                            newPlacement.ExpectedMaturityAmount,
                            newPlacement.Status,
                            newPlacement.MaturityForecastItemId,
                            newPlacement.ActivatedByUserId,
                            newPlacement.ActivatedAtUtc
                        },

                    Metadata =
                        new
                        {
                            Module =
                                "Investment Rollover",

                            RolloverRequestId =
                                request.Id,

                            OriginalInvestmentPlacementId =
                                originalPlacement.Id,

                            OriginalInvestmentReference =
                                originalPlacement.Reference
                        }
                });

            await RecordAudit(
                request,
                AuditActionTypes.Updated,
                $"Rollover request {request.Id} was " +
                $"executed successfully as investment " +
                $"{newPlacement.Reference}.");

            await _accountRepository.CommitTransaction();

            return Map(request);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository.RollbackTransaction();

            throw new ConflictException(
                "The rollover request, investment, forecast " +
                "or account changed during execution.");
        }
        catch (DbUpdateException)
        {
            await _accountRepository.RollbackTransaction();

            throw new ConflictException(
                "The investment rollover could not be saved. " +
                "It may already have been executed.");
        }
        catch
        {
            await _accountRepository.RollbackTransaction();

            throw;
        }
    }

    private static void ValidateOriginalPlacement(
        InvestmentRolloverRequest request,
        InvestmentPlacement placement)
    {
        if (placement.Id !=
            request.OriginalInvestmentPlacementId)
        {
            throw new ConflictException(
                "The loaded investment does not match the " +
                "rollover request.");
        }

        if (!string.Equals(
            placement.Reference,
            request.OriginalInvestmentReference,
            StringComparison.Ordinal))
        {
            throw new ConflictException(
                "The original investment reference changed " +
                "after the rollover was requested.");
        }

        if (placement.PrincipalAmount !=
            request.OriginalPrincipalAmount)
        {
            throw new ConflictException(
                "The original investment principal changed " +
                "after the rollover was requested.");
        }

        if (!string.Equals(
            placement.Currency,
            request.Currency,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "The original investment currency changed " +
                "after the rollover was requested.");
        }

        if (placement.MaturityDateUtc.Date !=
            request.OriginalMaturityDateUtc.Date)
        {
            throw new ConflictException(
                "The original investment maturity date " +
                "changed after the rollover was requested.");
        }
    }

    private static void ValidateExecutionPayoutAccount(
        InvestmentRolloverRequest request,
        Account? payoutAccount)
    {
        if (request.CashPayoutAmount <= 0)
        {
            if (request.CashPayoutAccountId.HasValue ||
                payoutAccount is not null)
            {
                throw new ConflictException(
                    "A rollover without a cash payout cannot " +
                    "have a payout account.");
            }

            return;
        }

        if (!request.CashPayoutAccountId.HasValue ||
            payoutAccount is null)
        {
            throw new ConflictException(
                "The rollover cash payout account was not loaded.");
        }

        if (payoutAccount.Id !=
            request.CashPayoutAccountId.Value)
        {
            throw new ConflictException(
                "The loaded payout account does not match " +
                "the rollover request.");
        }

        if (!payoutAccount.IsActive)
        {
            throw new ConflictException(
                "The rollover cash payout account is inactive.");
        }

        if (!string.Equals(
            payoutAccount.Currency,
            request.Currency,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "The payout account currency must match " +
                "the rollover currency.");
        }
    }

    private static void ValidateLockedAmounts(
        InvestmentRolloverRequest request)
    {
        var expectedGrossMaturity =
            RoundMoney(
                request.OriginalPrincipalAmount +
                request.GrossInterestAmount);

        var expectedWithholdingTax =
            RoundMoney(
                request.GrossInterestAmount *
                (request.WithholdingTaxRatePercentage /
                100m));

        var expectedNetInterest =
            RoundMoney(
                request.GrossInterestAmount -
                expectedWithholdingTax);

        var expectedNetMaturity =
            RoundMoney(
                request.OriginalPrincipalAmount +
                expectedNetInterest);

        decimal expectedRolloverPrincipal;
        decimal expectedCashPayout;

        if (request.RolloverOption ==
            InvestmentRolloverOptions.PrincipalOnly)
        {
            expectedRolloverPrincipal =
                request.OriginalPrincipalAmount;

            expectedCashPayout =
                expectedNetInterest;
        }
        else if (request.RolloverOption ==
            InvestmentRolloverOptions
                .PrincipalAndNetInterest)
        {
            expectedRolloverPrincipal =
                expectedNetMaturity;

            expectedCashPayout =
                0m;
        }
        else
        {
            throw new ConflictException(
                "The locked rollover option is invalid.");
        }

        var expectedTenorDays =
            (request.NewMaturityDateUtc.Date -
            request.NewStartDateUtc.Date).Days;

        if (expectedTenorDays <= 0 ||
            expectedTenorDays !=
                request.NewTenorDays)
        {
            throw new ConflictException(
                "The locked replacement investment tenor " +
                "is inconsistent.");
        }

        if (request.NewDayCountBasis != 360 &&
            request.NewDayCountBasis != 365)
        {
            throw new ConflictException(
                "The locked day-count basis is invalid.");
        }

        var expectedNewInterest =
            RoundMoney(
                expectedRolloverPrincipal *
                (request.NewAnnualInterestRate / 100m) *
                expectedTenorDays /
                request.NewDayCountBasis);

        var expectedNewMaturity =
            RoundMoney(
                expectedRolloverPrincipal +
                expectedNewInterest);

        if (request.GrossMaturityAmount !=
                expectedGrossMaturity ||
            request.WithholdingTaxAmount !=
                expectedWithholdingTax ||
            request.NetInterestAmount !=
                expectedNetInterest ||
            request.NetMaturityProceeds !=
                expectedNetMaturity ||
            request.RolloverPrincipalAmount !=
                expectedRolloverPrincipal ||
            request.CashPayoutAmount !=
                expectedCashPayout ||
            request.NewExpectedInterestAmount !=
                expectedNewInterest ||
            request.NewExpectedMaturityAmount !=
                expectedNewMaturity)
        {
            throw new ConflictException(
                "The locked rollover amounts are internally " +
                "inconsistent.");
        }
    }

    private async Task<string> GenerateRolloverReference()
    {
        for (var attempt = 0;
            attempt < 10;
            attempt++)
        {
            var reference =
                $"INV-RL-{DateTime.UtcNow:yyyyMMdd}-" +
                Guid.NewGuid()
                    .ToString("N")[..8]
                    .ToUpperInvariant();

            if (!await _placementRepository
                    .ReferenceExists(reference))
            {
                return reference;
            }
        }

        throw new ConflictException(
            "A unique rollover investment reference could " +
            "not be generated.");
    }

    private static decimal RoundMoney(
        decimal amount)
    {
        return Math.Round(
            amount,
            2,
            MidpointRounding.AwayFromZero);
    }

    private async Task<Account?> GetPayoutAccount(
        Guid? suppliedAccountId,
        InvestmentRolloverQuoteDto quote)
    {
        var accountId =
            suppliedAccountId.HasValue &&
            suppliedAccountId.Value != Guid.Empty
                ? suppliedAccountId
                : null;

        if (quote.CashPayoutAmount <= 0)
        {
            if (accountId.HasValue)
            {
                throw new BusinessRuleException(
                    "Cash payout account must be omitted " +
                    "when the rollover has no cash payout.");
            }

            return null;
        }

        if (!accountId.HasValue)
        {
            throw new RequestValidationException(
                "Cash payout account is required when " +
                "the rollover produces a cash payout.");
        }

        var account =
            await _accountRepository.GetById(
                accountId.Value);

        if (account is null)
        {
            throw new ResourceNotFoundException(
                "Cash payout account was not found.");
        }

        if (!account.IsActive)
        {
            throw new ConflictException(
                "The cash payout account is inactive.");
        }

        if (!string.Equals(
            account.Currency,
            quote.Currency,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "Cash payout account currency must match " +
                "the investment currency.");
        }

        return account;
    }

    private async Task EnsureNoOpenRequest(
        Guid investmentPlacementId)
    {
        var openRequest =
            await _requestRepository
                .GetOpenForPlacement(
                    investmentPlacementId);

        if (openRequest is null)
        {
            return;
        }

        /*
         * Release an expired pending request before a
         * replacement request is created.
         */
        if (openRequest.Status ==
                InvestmentRolloverStatuses.Pending &&
            openRequest.ExpiresAtUtc <=
                DateTime.UtcNow)
        {
            openRequest.Status =
                InvestmentRolloverStatuses.Expired;

            openRequest.ConcurrencyToken =
                Guid.NewGuid();

            await _requestRepository.SaveChanges();

            await RecordAudit(
                openRequest,
                AuditActionTypes.Expired,
                $"Rollover request {openRequest.Id} " +
                "expired before approval.");

            return;
        }

        throw new ConflictException(
            "An open rollover request already exists " +
            "for this investment placement.");
    }

    private async Task EnsurePending(
        InvestmentRolloverRequest request)
    {
        if (request.Status !=
            InvestmentRolloverStatuses.Pending)
        {
            throw new ConflictException(
                "Only a pending rollover request can " +
                "be reviewed.");
        }

        if (request.ExpiresAtUtc >
            DateTime.UtcNow)
        {
            return;
        }

        request.Status =
            InvestmentRolloverStatuses.Expired;

        request.ConcurrencyToken =
            Guid.NewGuid();

        await _requestRepository.SaveChanges();

        await RecordAudit(
            request,
            AuditActionTypes.Expired,
            $"Rollover request {request.Id} " +
            "expired before approval.");

        throw new ConflictException(
            "The rollover request has expired.");
    }

    private static void EnsureDifferentReviewer(
        InvestmentRolloverRequest request,
        Guid reviewerUserId)
    {
        if (request.RequestedByUserId ==
            reviewerUserId)
        {
            throw new ForbiddenOperationException(
                "You cannot approve or reject your own " +
                "rollover request.");
        }
    }

    private async Task<
        InvestmentRolloverRequest> GetRequest(
            Guid id)
    {
        var request =
            await _requestRepository.GetById(id);

        return request ??
            throw new ResourceNotFoundException(
                "Investment rollover request was not found.");
    }

    private async Task SaveReviewChanges()
    {
        try
        {
            await _requestRepository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The rollover request changed while it " +
                "was being reviewed.");
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The rollover decision could not be saved. " +
                "It may already have been submitted.");
        }
    }

    private Task RecordAudit(
        InvestmentRolloverRequest request,
        string action,
        string summary)
    {
        return _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action = action,

                EntityType =
                    AuditEntityTypes
                        .InvestmentRolloverRequest,

                EntityId = request.Id,

                EntityReference =
                    request.OriginalInvestmentReference,

                Summary = summary,

                Metadata = new
                {
                    Module =
                        "Investment Rollover",

                    request.OriginalInvestmentPlacementId,
                    request.RolloverOption,
                    request.Currency,
                    request.RolloverPrincipalAmount,
                    request.CashPayoutAmount,
                    request.NewInstitutionName,
                    request.NewMaturityDateUtc,
                    request.Status,
                    request.ApprovalCount,
                    request.RequiredApprovalCount,
                    request.ExpiresAtUtc,
                    request.NewInvestmentPlacementId,
                    request.CashPayoutTreasuryTransactionId,
                    request.ExecutedAtUtc
                }
            });
    }

    private static string NormalizeRequiredText(
        string? value,
        string fieldName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RequestValidationException(
                $"{fieldName} is required.");
        }

        var normalized =
            value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new RequestValidationException(
                $"{fieldName} cannot exceed " +
                $"{maximumLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized =
            value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new RequestValidationException(
                $"Text cannot exceed {maximumLength} " +
                "characters.");
        }

        return normalized;
    }

    private static InvestmentRolloverRequestResponseDto
        Map(InvestmentRolloverRequest request)
    {
        return new InvestmentRolloverRequestResponseDto
        {
            Id = request.Id,

            OriginalInvestmentPlacementId =
                request.OriginalInvestmentPlacementId,

            OriginalInvestmentReference =
                request.OriginalInvestmentReference,

            OriginalInstitutionName =
                request.OriginalInstitutionName,

            Currency =
                request.Currency,

            OriginalMaturityDateUtc =
                request.OriginalMaturityDateUtc,

            OriginalPrincipalAmount =
                request.OriginalPrincipalAmount,

            GrossInterestAmount =
                request.GrossInterestAmount,

            GrossMaturityAmount =
                request.GrossMaturityAmount,

            WithholdingTaxRatePercentage =
                request.WithholdingTaxRatePercentage,

            WithholdingTaxAmount =
                request.WithholdingTaxAmount,

            NetInterestAmount =
                request.NetInterestAmount,

            NetMaturityProceeds =
                request.NetMaturityProceeds,

            RolloverOption =
                request.RolloverOption,

            RolloverPrincipalAmount =
                request.RolloverPrincipalAmount,

            CashPayoutAmount =
                request.CashPayoutAmount,

            CashPayoutAccountId =
                request.CashPayoutAccountId,

            CashPayoutAccountName =
                request.CashPayoutAccount?.Name,

            NewInvestmentType =
                request.NewInvestmentType,

            NewInstitutionName =
                request.NewInstitutionName,

            NewAnnualInterestRate =
                request.NewAnnualInterestRate,

            NewDayCountBasis =
                request.NewDayCountBasis,

            NewStartDateUtc =
                request.NewStartDateUtc,

            NewMaturityDateUtc =
                request.NewMaturityDateUtc,

            NewTenorDays =
                request.NewTenorDays,

            NewExpectedInterestAmount =
                request.NewExpectedInterestAmount,

            NewExpectedMaturityAmount =
                request.NewExpectedMaturityAmount,

            RequestIdempotencyKey =
                request.RequestIdempotencyKey,

            ExecutionIdempotencyKey =
                request.ExecutionIdempotencyKey,

            ExternalReference =
                request.ExternalReference,

            Notes =
                request.Notes,

            Status =
                request.Status,

            RequiredApprovalCount =
                request.RequiredApprovalCount,

            ApprovalCount =
                request.ApprovalCount,

            RemainingApprovalCount =
                Math.Max(
                    0,
                    request.RequiredApprovalCount -
                    request.ApprovalCount),

            RequestedByUserId =
                request.RequestedByUserId,

            RequestedAtUtc =
                request.RequestedAtUtc,

            ExpiresAtUtc =
                request.ExpiresAtUtc,

            RejectedByUserId =
                request.RejectedByUserId,

            RejectedAtUtc =
                request.RejectedAtUtc,

            RejectionReason =
                request.RejectionReason,

            NewInvestmentPlacementId =
                request.NewInvestmentPlacementId,
            
            NewInvestmentReference =
                request.NewInvestmentPlacement?.Reference,

            CashPayoutTreasuryTransactionId =
                request.CashPayoutTreasuryTransactionId,

            CashPayoutTransactionReference =
                request.CashPayoutTreasuryTransaction?.Reference,

            ExecutedByUserId =
                request.ExecutedByUserId,

            ExecutedAtUtc =
                request.ExecutedAtUtc,

            Decisions =
                request.Decisions
                    .OrderBy(decision =>
                        decision.CreatedAtUtc)
                    .Select(decision =>
                        new InvestmentRolloverDecisionDto
                        {
                            Id =
                                decision.Id,

                            ApproverUserId =
                                decision.ApproverUserId,

                            Decision =
                                decision.Decision,

                            Comment =
                                decision.Comment,

                            CreatedAtUtc =
                                decision.CreatedAtUtc
                        })
                    .ToList()
        };
    }
}