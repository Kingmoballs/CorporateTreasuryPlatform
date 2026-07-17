using Microsoft.EntityFrameworkCore;
using Treasury.Shared.Common;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class InvestmentEarlyRedemptionRequestService
    : IInvestmentEarlyRedemptionRequestService
{
    private readonly IInvestmentEarlyRedemptionRequestRepository
        _requestRepository;

    private readonly IInvestmentEarlyRedemptionService
        _quoteService;

    private readonly IAccountRepository
        _accountRepository;

    private readonly IApprovalPolicyService
        _approvalPolicyService;

    private readonly ITreasuryTransactionRepository
        _transactionRepository;

    private readonly ILedgerRepository
        _ledgerRepository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuditLogService
        _auditLogService;

    public InvestmentEarlyRedemptionRequestService(
        IInvestmentEarlyRedemptionRequestRepository requestRepository,
        IInvestmentEarlyRedemptionService quoteService,
        IAccountRepository accountRepository,
        IApprovalPolicyService approvalPolicyService,
        ITreasuryTransactionRepository transactionRepository,
        ILedgerRepository ledgerRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _requestRepository = requestRepository;
        _quoteService = quoteService;
        _accountRepository = accountRepository;
        _approvalPolicyService = approvalPolicyService;
        _transactionRepository = transactionRepository;
        _ledgerRepository = ledgerRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<
        InvestmentEarlyRedemptionRequestResponseDto>
        Create(
            Guid investmentPlacementId,
            CreateInvestmentEarlyRedemptionRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (investmentPlacementId == Guid.Empty)
        {
            throw new RequestValidationException(
                "A valid investment placement ID is required.");
        }

        if (dto.DestinationAccountId == Guid.Empty)
        {
            throw new RequestValidationException(
                "A destination account is required.");
        }

        var idempotencyKey =
            NormalizeIdempotencyKey(
                dto.IdempotencyKey);

        var existingRequest =
            await _requestRepository
                .GetByIdempotencyKey(
                    idempotencyKey);

        if (existingRequest is not null)
        {
            if (existingRequest.InvestmentPlacementId !=
                investmentPlacementId)
            {
                throw new ConflictException(
                    "The idempotency key has already been " +
                    "used for another investment.");
            }

            return Map(existingRequest);
        }

        var quote =
            await _quoteService.GetQuote(
                investmentPlacementId,
                new InvestmentEarlyRedemptionQuoteRequestDto
                {
                    ProposedRedemptionDateUtc =
                        dto.ProposedRedemptionDateUtc,

                    PenaltyRatePercentage =
                        dto.PenaltyRatePercentage,

                    WithholdingTaxRatePercentage =
                        dto.WithholdingTaxRatePercentage
                });

        var destinationAccount =
            await _accountRepository.GetById(
                dto.DestinationAccountId);

        if (destinationAccount is null)
        {
            throw new ResourceNotFoundException(
                "Destination account was not found.");
        }

        if (!destinationAccount.IsActive)
        {
            throw new ConflictException(
                "The destination account is inactive.");
        }

        if (!string.Equals(
                destinationAccount.Currency,
                quote.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "The destination account currency must " +
                "match the investment currency.");
        }

        var approvalRequirements =
            await _approvalPolicyService
                .GetRequirements(
                    ApprovalOperationTypes
                        .InvestmentEarlyRedemption,
                    quote.Currency);

        var nowUtc =
            DateTime.UtcNow;

        var requestId =
            Guid.NewGuid();

        var request =
            new InvestmentEarlyRedemptionRequest
            {
                Id =
                    requestId,

                InvestmentPlacementId =
                    investmentPlacementId,

                InvestmentReference =
                    quote.InvestmentReference,

                InstitutionName =
                    quote.InstitutionName,

                DestinationAccountId =
                    destinationAccount.Id,

                DestinationAccount =
                    destinationAccount,

                Currency =
                    quote.Currency,

                ProposedRedemptionDateUtc =
                    quote.ProposedRedemptionDateUtc,

                PrincipalAmount =
                    quote.PrincipalAmount,

                GrossAccruedInterestAmount =
                    quote.GrossAccruedInterestAmount,

                PenaltyRatePercentage =
                    quote.PenaltyRatePercentage,

                PenaltyAmount =
                    quote.PenaltyAmount,

                InterestAfterPenaltyAmount =
                    quote.InterestAfterPenaltyAmount,

                WithholdingTaxRatePercentage =
                    quote.WithholdingTaxRatePercentage,

                WithholdingTaxAmount =
                    quote.WithholdingTaxAmount,

                NetInterestAmount =
                    quote.NetInterestAmount,

                EstimatedRedemptionProceeds =
                    quote.EstimatedRedemptionProceeds,

                ExpectedProceedsShortfall =
                    quote.ExpectedProceedsShortfall,

                RequestIdempotencyKey =
                    idempotencyKey,

                ExecutionIdempotencyKey =
                    $"EARLY-REDEMPTION-" +
                    $"{requestId:N}",

                ExternalReference =
                    NormalizeOptionalText(
                        dto.ExternalReference,
                        100),

                Notes =
                    NormalizeOptionalText(
                        dto.Notes,
                        1000),

                Status =
                    InvestmentEarlyRedemptionStatuses
                        .Pending,

                RequiredApprovalCount =
                    approvalRequirements
                        .RequiredApprovalCount,

                ApprovalCount =
                    0,

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

        await _requestRepository.Add(
            request);

        await _requestRepository.SaveChanges();

        await RecordAudit(
            request,
            AuditActionTypes.Created,
            $"Early-redemption request {request.Id} " +
            $"was created for investment " +
            $"{request.InvestmentReference}.");

        return Map(request);
    }

    public async Task<
        InvestmentEarlyRedemptionRequestResponseDto>
        GetById(Guid id)
    {
        var request =
            await GetRequest(id);

        return Map(request);
    }

    public async Task<List<
        InvestmentEarlyRedemptionRequestResponseDto>>
        GetPending()
    {
        var requests =
            await _requestRepository.GetPending();

        return requests
            .Select(Map)
            .ToList();
    }

    public async Task<
        InvestmentEarlyRedemptionRequestResponseDto>
        Approve(Guid id)
    {
        var request =
            await GetRequest(id);

        await EnsurePending(
            request);

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
                "You have already reviewed this request.");
        }

        var decision =
            new InvestmentEarlyRedemptionDecision
            {
                Id =
                    Guid.NewGuid(),

                InvestmentEarlyRedemptionRequestId =
                    request.Id,

                InvestmentEarlyRedemptionRequest =
                    request,

                ApproverUserId =
                    currentUserId,

                Decision =
                    ApprovalDecisionTypes.Approved,

                CreatedAtUtc =
                    DateTime.UtcNow
            };

        await _requestRepository.AddDecision(
            decision);

        request.ApprovalCount += 1;

        if (request.ApprovalCount >=
            request.RequiredApprovalCount)
        {
            request.Status =
                InvestmentEarlyRedemptionStatuses
                    .Approved;
        }

        request.ConcurrencyToken =
            Guid.NewGuid();

        _requestRepository.Update(
            request);

        await _requestRepository.SaveChanges();

        await RecordAudit(
            request,
            AuditActionTypes.Approved,
            request.Status ==
                InvestmentEarlyRedemptionStatuses.Approved
                ? $"Early-redemption request {request.Id} " +
                  "received final approval."
                : $"Early-redemption request {request.Id} " +
                  "received partial approval.");

        return Map(request);
    }

    public async Task<
        InvestmentEarlyRedemptionRequestResponseDto>
        Reject(
            Guid id,
            string reason)
    {
        var request =
            await GetRequest(id);

        await EnsurePending(
            request);

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
                "You have already reviewed this request.");
        }

        var normalizedReason =
            NormalizeRequiredText(
                reason,
                "Rejection reason",
                500);

        var rejectedAtUtc =
            DateTime.UtcNow;

        var decision =
            new InvestmentEarlyRedemptionDecision
            {
                Id =
                    Guid.NewGuid(),

                InvestmentEarlyRedemptionRequestId =
                    request.Id,

                InvestmentEarlyRedemptionRequest =
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

        await _requestRepository.AddDecision(
            decision);

        request.Status =
            InvestmentEarlyRedemptionStatuses.Rejected;

        request.RejectedByUserId =
            currentUserId;

        request.RejectedAtUtc =
            rejectedAtUtc;

        request.RejectionReason =
            normalizedReason;

        request.ConcurrencyToken =
            Guid.NewGuid();

        _requestRepository.Update(
            request);

        await _requestRepository.SaveChanges();

        await RecordAudit(
            request,
            AuditActionTypes.Rejected,
            $"Early-redemption request {request.Id} " +
            "was rejected.");

        return Map(request);
    }

    public async Task<
        InvestmentEarlyRedemptionRequestResponseDto>
        Execute(Guid id)
    {
        await _accountRepository.BeginTransaction();

        try
        {
            var request =
                await GetRequest(id);

            /*
            * A retry after a successful commit returns the
            * original result without crediting cash again.
            */
            if (request.Status ==
                InvestmentEarlyRedemptionStatuses.Executed)
            {
                await _accountRepository.CommitTransaction();

                return Map(request);
            }

            if (request.Status !=
                InvestmentEarlyRedemptionStatuses.Approved)
            {
                throw new ConflictException(
                    "Only an approved early-redemption " +
                    "request can be executed.");
            }

            if (request.ApprovalCount <
                request.RequiredApprovalCount)
            {
                throw new ConflictException(
                    "The early-redemption request has not " +
                    "received all required approvals.");
            }

            var executedByUserId =
                _currentUserService.UserId;

            if (request.RequestedByUserId ==
                executedByUserId)
            {
                throw new ForbiddenOperationException(
                    "The requester cannot execute their own " +
                    "early-redemption request.");
            }

            var nowUtc =
                DateTime.UtcNow;

            if (request.ProposedRedemptionDateUtc.Date >
                nowUtc.Date)
            {
                throw new ConflictException(
                    "The proposed early-redemption date " +
                    "has not been reached.");
            }

            var placement =
                request.InvestmentPlacement;

            if (placement is null)
            {
                throw new ConflictException(
                    "The investment placement was not loaded.");
            }

            if (placement.Status !=
                InvestmentPlacementStatuses.Active)
            {
                throw new ConflictException(
                    "Only an active investment can be " +
                    "executed through early redemption.");
            }

            if (placement.MaturityDateUtc <= nowUtc)
            {
                throw new ConflictException(
                    "The investment has reached maturity. " +
                    "Use the normal maturity-redemption process.");
            }

            if (placement.Id !=
                request.InvestmentPlacementId)
            {
                throw new ConflictException(
                    "The early-redemption request does not " +
                    "match the loaded investment.");
            }

            if (placement.PrincipalAmount !=
                request.PrincipalAmount)
            {
                throw new ConflictException(
                    "The investment principal has changed " +
                    "since the request was approved.");
            }

            if (!string.Equals(
                    placement.Currency,
                    request.Currency,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    "The investment currency has changed " +
                    "since the request was approved.");
            }

            ValidateLockedAmounts(request);

            var destinationAccount =
                request.DestinationAccount;

            if (destinationAccount is null)
            {
                throw new ConflictException(
                    "The destination account was not loaded.");
            }

            if (!destinationAccount.IsActive)
            {
                throw new ConflictException(
                    "The destination account is inactive.");
            }

            if (!string.Equals(
                    destinationAccount.Currency,
                    request.Currency,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException(
                    "The destination account currency must " +
                    "match the investment currency.");
            }

            var maturityForecast =
                placement.MaturityForecastItem;

            if (maturityForecast is null)
            {
                throw new ConflictException(
                    "The investment maturity forecast " +
                    "was not loaded.");
            }

            if (maturityForecast.Status !=
                CashFlowForecastStatus.Active)
            {
                throw new ConflictException(
                    "The investment maturity forecast has " +
                    "already been processed.");
            }

            var existingTransaction =
                await _transactionRepository
                    .GetByIdempotencyKey(
                        request.ExecutionIdempotencyKey);

            if (existingTransaction is not null)
            {
                /*
                * Request and financial execution are committed
                * atomically. An existing transaction while the
                * request is not Executed is an invariant breach.
                */
                throw new ConflictException(
                    "A transaction already exists for this " +
                    "early-redemption execution key.");
            }

            var placementBeforeValues =
                new
                {
                    placement.Id,
                    placement.Reference,
                    placement.Status,
                    placement.PrincipalAmount,
                    placement.RedemptionAccountId,
                    placement.RedemptionTreasuryTransactionId,
                    placement.ActualInterestAmount,
                    placement.WithholdingTaxAmount,
                    placement.ActualMaturityAmount,
                    placement.RedeemedAtUtc
                };

            var description =
                $"Early-redemption proceeds for investment " +
                $"{placement.Reference} from " +
                $"{placement.InstitutionName}.";

            var transaction =
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
                        request.EstimatedRedemptionProceeds,

                    Currency =
                        request.Currency,

                    Description =
                        description,

                    SourceAccountId =
                        null,

                    DestinationAccountId =
                        destinationAccount.Id,

                    Category =
                        "Investment Early Redemption",

                    CounterpartyName =
                        request.InstitutionName,

                    ExternalReference =
                        request.ExternalReference ??
                        request.InvestmentReference,

                    IdempotencyKey =
                        request.ExecutionIdempotencyKey,

                    InitiatedByUserId =
                        executedByUserId,

                    CompletedByUserId =
                        executedByUserId,

                    CreatedAtUtc =
                        nowUtc,

                    CompletedAtUtc =
                        nowUtc
                };

            /*
            * Early-redemption proceeds are entering the bank
            * account, so the bank-account asset is debited.
            */
            destinationAccount.Balance +=
                request.EstimatedRedemptionProceeds;

            destinationAccount.ConcurrencyToken =
                Guid.NewGuid();

            var ledgerEntry =
                new LedgerEntry
                {
                    Id =
                        Guid.NewGuid(),

                    AccountId =
                        destinationAccount.Id,

                    TreasuryTransactionId =
                        transaction.Id,

                    Amount =
                        request.EstimatedRedemptionProceeds,

                    EntryType =
                        "Debit",

                    Description =
                        description,

                    CreatedAt =
                        nowUtc
                };

            maturityForecast.Status =
                CashFlowForecastStatus.Realized;

            maturityForecast.RealizedTreasuryTransactionId =
                transaction.Id;

            maturityForecast.RealizedTreasuryTransaction =
                transaction;

            maturityForecast.RealizedAtUtc =
                nowUtc;

            maturityForecast.UpdatedAtUtc =
                nowUtc;

            maturityForecast.ConcurrencyToken =
                Guid.NewGuid();

            /*
            * Interest after penalty is the actual gross
            * interest recognised on the placement. Tax is
            * then deducted to obtain the net cash interest.
            */
            placement.Status =
                InvestmentPlacementStatuses.Redeemed;

            placement.RedemptionIdempotencyKey =
                request.ExecutionIdempotencyKey;

            placement.RedemptionAccountId =
                destinationAccount.Id;

            placement.RedemptionAccount =
                destinationAccount;

            placement.RedemptionTreasuryTransactionId =
                transaction.Id;

            placement.RedemptionTreasuryTransaction =
                transaction;

            placement.ActualInterestAmount =
                request.InterestAfterPenaltyAmount;

            placement.WithholdingTaxAmount =
                request.WithholdingTaxAmount;

            placement.ActualMaturityAmount =
                request.EstimatedRedemptionProceeds;

            placement.RedemptionExternalReference =
                request.ExternalReference;

            placement.RedemptionNotes =
                request.Notes;

            placement.RedeemedByUserId =
                executedByUserId;

            placement.RedeemedAtUtc =
                nowUtc;

            placement.UpdatedAtUtc =
                nowUtc;

            placement.ConcurrencyToken =
                Guid.NewGuid();

            request.Status =
                InvestmentEarlyRedemptionStatuses.Executed;

            request.RedemptionTreasuryTransactionId =
                transaction.Id;

            request.RedemptionTreasuryTransaction =
                transaction;

            request.ExecutedAtUtc =
                nowUtc;

            request.ConcurrencyToken =
                Guid.NewGuid();

            await _transactionRepository.Add(
                transaction);

            await _ledgerRepository.Add(
                ledgerEntry);

            /*
            * The request, placement, account and forecast were
            * loaded as tracked entities. Do not call Update on
            * the whole request graph because approval decisions
            * are immutable.
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
                        placement.Id,

                    EntityReference =
                        placement.Reference,

                    Summary =
                        $"Investment placement " +
                        $"{placement.Reference} was redeemed " +
                        "before maturity.",

                    BeforeValues =
                        placementBeforeValues,

                    AfterValues =
                        new
                        {
                            placement.Id,
                            placement.Reference,
                            placement.Status,
                            placement.RedemptionAccountId,
                            placement
                                .RedemptionTreasuryTransactionId,
                            placement.ActualInterestAmount,
                            placement.WithholdingTaxAmount,
                            placement.ActualMaturityAmount,
                            placement.RedeemedByUserId,
                            placement.RedeemedAtUtc
                        },

                    Metadata =
                        new
                        {
                            Module =
                                "Investment Early Redemption",

                            EarlyRedemptionRequestId =
                                request.Id,

                            TransactionId =
                                transaction.Id,

                            TransactionReference =
                                transaction.Reference,

                            request.GrossAccruedInterestAmount,
                            request.PenaltyRatePercentage,
                            request.PenaltyAmount,
                            request.InterestAfterPenaltyAmount,
                            request.WithholdingTaxAmount,
                            request.NetInterestAmount,
                            request.EstimatedRedemptionProceeds
                        }
                });

            await RecordAudit(
                request,
                AuditActionTypes.Redeemed,
                $"Early-redemption request {request.Id} " +
                "was executed successfully.");

            await _accountRepository.CommitTransaction();

            return Map(request);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository.RollbackTransaction();

            throw new ConflictException(
                "The request, investment, forecast or " +
                "destination account changed during " +
                "early-redemption execution.");
        }
        catch (DbUpdateException)
        {
            await _accountRepository.RollbackTransaction();

            throw new ConflictException(
                "The early-redemption execution could not " +
                "be saved. It may already have been processed.");
        }
        catch
        {
            await _accountRepository.RollbackTransaction();

            throw;
        }
    }

    private async Task EnsurePending(
        InvestmentEarlyRedemptionRequest request)
    {
        if (request.Status !=
            InvestmentEarlyRedemptionStatuses.Pending)
        {
            throw new ConflictException(
                "Only a pending early-redemption " +
                "request can be reviewed.");
        }

        if (request.ExpiresAtUtc >
            DateTime.UtcNow)
        {
            return;
        }

        request.Status =
            InvestmentEarlyRedemptionStatuses.Expired;

        request.ConcurrencyToken =
            Guid.NewGuid();

        _requestRepository.Update(
            request);

        await _requestRepository.SaveChanges();

        await RecordAudit(
            request,
            AuditActionTypes.Expired,
            $"Early-redemption request {request.Id} " +
            "expired before approval.");

        throw new ConflictException(
            "The early-redemption request has expired.");
    }

    private static void EnsureDifferentReviewer(
        InvestmentEarlyRedemptionRequest request,
        Guid reviewerUserId)
    {
        if (request.RequestedByUserId ==
            reviewerUserId)
        {
            throw new ForbiddenOperationException(
                "You cannot approve or reject your own " +
                "early-redemption request.");
        }
    }

    private async Task<
        InvestmentEarlyRedemptionRequest> GetRequest(
            Guid id)
    {
        var request =
            await _requestRepository.GetById(id);

        return request ??
            throw new ResourceNotFoundException(
                "Early-redemption request was not found.");
    }

    private Task RecordAudit(
        InvestmentEarlyRedemptionRequest request,
        string action,
        string summary)
    {
        return _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    action,

                EntityType =
                    AuditEntityTypes
                        .InvestmentEarlyRedemptionRequest,

                EntityId =
                    request.Id,

                EntityReference =
                    request.InvestmentReference,

                Summary =
                    summary,

                Metadata =
                    new
                    {
                        Module =
                            "Investment Early Redemption",

                        request.InvestmentPlacementId,
                        request.DestinationAccountId,
                        request.Currency,
                        request.PrincipalAmount,
                        request.EstimatedRedemptionProceeds,
                        request.Status,
                        request.ApprovalCount,
                        request.RequiredApprovalCount,
                        request.ExpiresAtUtc,
                        request.RedemptionTreasuryTransactionId,
                        request.ExecutedAtUtc
                    }
            });
    }

    private static void ValidateLockedAmounts(
        InvestmentEarlyRedemptionRequest request)
    {
        var expectedPenaltyAmount =
            RoundMoney(
                request.GrossAccruedInterestAmount *
                (request.PenaltyRatePercentage / 100m));

        var expectedInterestAfterPenalty =
            RoundMoney(
                request.GrossAccruedInterestAmount -
                expectedPenaltyAmount);

        var expectedWithholdingTax =
            RoundMoney(
                expectedInterestAfterPenalty *
                (request.WithholdingTaxRatePercentage /
                100m));

        var expectedNetInterest =
            RoundMoney(
                expectedInterestAfterPenalty -
                expectedWithholdingTax);

        var expectedRedemptionProceeds =
            RoundMoney(
                request.PrincipalAmount +
                expectedNetInterest);

        if (request.PenaltyAmount !=
                expectedPenaltyAmount ||
            request.InterestAfterPenaltyAmount !=
                expectedInterestAfterPenalty ||
            request.WithholdingTaxAmount !=
                expectedWithholdingTax ||
            request.NetInterestAmount !=
                expectedNetInterest ||
            request.EstimatedRedemptionProceeds !=
                expectedRedemptionProceeds)
        {
            throw new ConflictException(
                "The locked early-redemption amounts are " +
                "internally inconsistent.");
        }

        if (request.EstimatedRedemptionProceeds <= 0)
        {
            throw new ConflictException(
                "Early-redemption proceeds must be positive.");
        }
    }

    private static decimal RoundMoney(
        decimal amount)
    {
        return Math.Round(
            amount,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static string NormalizeIdempotencyKey(
        string? value)
    {
        return NormalizeRequiredText(
            value,
            "Idempotency key",
            100);
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

    private static
        InvestmentEarlyRedemptionRequestResponseDto Map(
            InvestmentEarlyRedemptionRequest request)
    {
        return new
            InvestmentEarlyRedemptionRequestResponseDto
            {
                Id =
                    request.Id,

                InvestmentPlacementId =
                    request.InvestmentPlacementId,

                InvestmentReference =
                    request.InvestmentReference,

                InstitutionName =
                    request.InstitutionName,

                DestinationAccountId =
                    request.DestinationAccountId,

                DestinationAccountName =
                    request.DestinationAccount?.Name,

                Currency =
                    request.Currency,

                ProposedRedemptionDateUtc =
                    request.ProposedRedemptionDateUtc,

                PrincipalAmount =
                    request.PrincipalAmount,

                GrossAccruedInterestAmount =
                    request.GrossAccruedInterestAmount,

                PenaltyRatePercentage =
                    request.PenaltyRatePercentage,

                PenaltyAmount =
                    request.PenaltyAmount,

                InterestAfterPenaltyAmount =
                    request.InterestAfterPenaltyAmount,

                WithholdingTaxRatePercentage =
                    request.WithholdingTaxRatePercentage,

                WithholdingTaxAmount =
                    request.WithholdingTaxAmount,

                NetInterestAmount =
                    request.NetInterestAmount,

                EstimatedRedemptionProceeds =
                    request.EstimatedRedemptionProceeds,

                ExpectedProceedsShortfall =
                    request.ExpectedProceedsShortfall,

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

                RedemptionTreasuryTransactionId =
                    request.RedemptionTreasuryTransactionId,

                ExecutedAtUtc =
                    request.ExecutedAtUtc,

                Decisions =
                    request.Decisions
                        .OrderBy(decision =>
                            decision.CreatedAtUtc)
                        .Select(decision =>
                            new
                                InvestmentEarlyRedemptionDecisionDto
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