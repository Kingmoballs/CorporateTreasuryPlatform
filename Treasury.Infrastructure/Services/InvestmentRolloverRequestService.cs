using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

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

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuditLogService
        _auditLogService;

    public InvestmentRolloverRequestService(
        IInvestmentRolloverRequestRepository requestRepository,
        IInvestmentRolloverService quoteService,
        IAccountRepository accountRepository,
        IApprovalPolicyService approvalPolicyService,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _requestRepository = requestRepository;
        _quoteService = quoteService;
        _accountRepository = accountRepository;
        _approvalPolicyService = approvalPolicyService;
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

            CashPayoutTreasuryTransactionId =
                request.CashPayoutTreasuryTransactionId,

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