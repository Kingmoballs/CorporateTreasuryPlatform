using Microsoft.EntityFrameworkCore;
using System.Text;
using Treasury.Shared.Common;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Exports;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class InvestmentPlacementService
    : IInvestmentPlacementService
{
    private static readonly string[] AllowedStatuses =
    {
        InvestmentPlacementStatuses.Draft,
        InvestmentPlacementStatuses.Active,
        InvestmentPlacementStatuses.Matured,
        InvestmentPlacementStatuses.Redeemed,
        InvestmentPlacementStatuses.Cancelled,
        InvestmentPlacementStatuses.PendingActivation,
        InvestmentPlacementStatuses.ActivationRejected,
        InvestmentPlacementStatuses.ActivationExpired,
    };

    private readonly IInvestmentPlacementRepository
        _placementRepository;
    
    private readonly ICounterpartyRepository
        _counterpartyRepository;

    private readonly IAccountRepository
        _accountRepository;
    
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
    
    private readonly IApprovalPolicyService
        _approvalPolicyService;

    private readonly IApprovalDecisionRepository
        _approvalDecisionRepository;
    
    private readonly IInvestmentLimitEnforcementService
        _limitEnforcementService;

    public InvestmentPlacementService(
        IInvestmentPlacementRepository placementRepository,
        ICounterpartyRepository counterpartyRepository,
        IAccountRepository accountRepository,
        ITreasuryTransactionRepository transactionRepository,
        ILedgerRepository ledgerRepository,
        ICashFlowForecastRepository forecastRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IApprovalPolicyService approvalPolicyService,
        IApprovalDecisionRepository approvalDecisionRepository,
        IInvestmentLimitEnforcementService limitEnforcementService)
    {
        _placementRepository =
            placementRepository;

        _counterpartyRepository =
            counterpartyRepository;

        _accountRepository =
            accountRepository;

        _transactionRepository =
            transactionRepository;

        _ledgerRepository =
            ledgerRepository;

        _forecastRepository =
            forecastRepository;

        _currentUserService =
            currentUserService;

        _auditLogService =
            auditLogService;

        _approvalPolicyService =
            approvalPolicyService;

        _approvalDecisionRepository =
            approvalDecisionRepository;
        
        _limitEnforcementService =
            limitEnforcementService;
    }

    public async Task<InvestmentPlacementResponseDto>
        Create(CreateInvestmentPlacementDto dto)
    {
        ValidateCreateRequest(dto);

        var account =
            await _accountRepository.GetById(
                dto.SourceAccountId);

        if (account is null)
        {
            throw new ResourceNotFoundException(
                "Source account was not found.");
        }

        if (!account.IsActive)
        {
            throw new ConflictException(
                "The source account is inactive.");
        }

        var investmentType =
            NormalizeInvestmentType(
                dto.InvestmentType);

        var counterparty =
            await _counterpartyRepository
                .GetById(dto.CounterpartyId);

        if (counterparty is null)
        {
            throw new ResourceNotFoundException(
                "Investment counterparty was not found.");
        }

        if (!counterparty.IsActive)
        {
            throw new ConflictException(
                "The selected investment counterparty " +
                "is inactive.");
        }

        var startDateUtc =
            NormalizeUtc(dto.StartDateUtc).Date;

        var maturityDateUtc =
            NormalizeUtc(dto.MaturityDateUtc).Date;

        if (maturityDateUtc <= startDateUtc)
        {
            throw new BusinessRuleException(
                "Maturity date must be later than the start date.");
        }

        if (maturityDateUtc <= DateTime.UtcNow.Date)
        {
            throw new BusinessRuleException(
                "Maturity date must be in the future.");
        }

        var tenorDays =
            (maturityDateUtc - startDateUtc).Days;

        if (tenorDays > 3650)
        {
            throw new BusinessRuleException(
                "Investment tenor cannot exceed 10 years.");
        }

        /*
         * Fixed-deposit interest is calculated using:
         * Principal × annual rate × tenor days ÷ day-count basis.
         */
        var expectedInterestAmount =
            Math.Round(
                dto.PrincipalAmount *
                (dto.AnnualInterestRate / 100m) *
                tenorDays /
                dto.DayCountBasis,
                2,
                MidpointRounding.AwayFromZero);

        var reference =
            await GenerateReference();

        var now =
            DateTime.UtcNow;

        var placement =
            new InvestmentPlacement
            {
                Id = Guid.NewGuid(),

                Reference = reference,

                InvestmentType =
                    investmentType,

                InstitutionName =
                    counterparty.Name,

                CounterpartyId =
                    counterparty.Id,

                Counterparty =
                    counterparty,
                SourceAccountId =
                    account.Id,

                SourceAccount =
                    account,

                PrincipalAmount =
                    dto.PrincipalAmount,

                // Currency is derived from the source account
                // to prevent cross-currency inconsistencies.
                Currency =
                    account.Currency.Trim().ToUpperInvariant(),

                AnnualInterestRate =
                    dto.AnnualInterestRate,

                DayCountBasis =
                    dto.DayCountBasis,

                StartDateUtc =
                    startDateUtc,

                MaturityDateUtc =
                    maturityDateUtc,

                ExpectedInterestAmount =
                    expectedInterestAmount,

                ExpectedMaturityAmount =
                    dto.PrincipalAmount +
                    expectedInterestAmount,

                /*
                 * A new placement remains Draft until the funding
                 * operation is completed in the next stage.
                 */
                Status =
                    InvestmentPlacementStatuses.Draft,

                ExternalReference =
                    NormalizeOptionalText(
                        dto.ExternalReference,
                        100),

                Notes =
                    NormalizeOptionalText(
                        dto.Notes,
                        1000),

                CreatedByUserId =
                    _currentUserService.UserId,

                CreatedAtUtc =
                    now,

                UpdatedAtUtc =
                    now,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        await _placementRepository.Add(placement);

        await _placementRepository.SaveChanges();

        var result =
            Map(placement);

        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Created,

                EntityType =
                    AuditEntityTypes.InvestmentPlacement,

                EntityId =
                    placement.Id,

                EntityReference =
                    placement.Reference,

                Summary =
                    $"Investment placement {placement.Reference} was created.",

                AfterValues =
                    Snapshot(placement),

                Metadata =
                    new
                    {
                        Module = "Investment Placements"
                    }
            });

        return result;
    }

    public async Task<InvestmentPlacementResponseDto>
        GetById(Guid id)
    {
        var placement =
            await _placementRepository.GetById(id);

        if (placement is null)
        {
            throw new ResourceNotFoundException(
                "Investment placement was not found.");
        }

        return Map(placement);
    }

    public async Task<InvestmentPlacementResponseDto>
        AssignCounterparty(
            Guid id,
            Guid counterpartyId)
    {
        if (counterpartyId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Counterparty ID is required.");
        }

        var placement =
            await _placementRepository.GetById(id);

        if (placement is null)
        {
            throw new ResourceNotFoundException(
                "Investment placement was not found.");
        }

        var counterparty =
            await _counterpartyRepository
                .GetById(counterpartyId);

        if (counterparty is null)
        {
            throw new ResourceNotFoundException(
                "Counterparty was not found.");
        }

        if (placement.CounterpartyId ==
            counterparty.Id)
        {
            return Map(placement);
        }

        var beforeValues =
            Snapshot(placement);

        placement.CounterpartyId =
            counterparty.Id;

        placement.Counterparty =
            counterparty;

        placement.InstitutionName =
            counterparty.Name;

        placement.UpdatedAtUtc =
            DateTime.UtcNow;

        placement.ConcurrencyToken =
            Guid.NewGuid();

        _placementRepository.Update(placement);

        try
        {
            await _placementRepository.SaveChanges();
        }
        catch (Microsoft.EntityFrameworkCore
            .DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The investment placement changed while " +
                "its counterparty was being assigned.");
        }

        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Updated,

                EntityType =
                    AuditEntityTypes.InvestmentPlacement,

                EntityId =
                    placement.Id,

                EntityReference =
                    placement.Reference,

                Summary =
                    $"Counterparty {counterparty.Code} was " +
                    $"assigned to investment placement " +
                    $"{placement.Reference}.",

                BeforeValues =
                    beforeValues,

                AfterValues =
                    Snapshot(placement),

                Metadata =
                    new
                    {
                        Module =
                            "Investment Counterparty Assignment",

                        counterparty.Id,

                        counterparty.Code
                    }
            });

        return Map(placement);
    }

    public async Task<PagedInvestmentPlacementResponseDto>
        Search(InvestmentPlacementQueryDto query)
    {
        query.Page =
            query.Page < 1 ? 1 : query.Page;

        query.PageSize =
            query.PageSize < 1
                ? 50
                : Math.Min(query.PageSize, 100);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            query.Status =
                NormalizeStatus(query.Status);
        }

        if (!string.IsNullOrWhiteSpace(
                query.InvestmentType))
        {
            query.InvestmentType =
                NormalizeInvestmentType(
                    query.InvestmentType);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            query.Currency =
                NormalizeCurrency(query.Currency);
        }

        if (query.MaturityFromUtc.HasValue)
        {
            query.MaturityFromUtc =
                NormalizeUtc(
                    query.MaturityFromUtc.Value);
        }

        if (query.MaturityToUtc.HasValue)
        {
            query.MaturityToUtc =
                NormalizeUtc(
                    query.MaturityToUtc.Value);
        }

        if (query.MaturityFromUtc.HasValue &&
            query.MaturityToUtc.HasValue &&
            query.MaturityFromUtc.Value >
            query.MaturityToUtc.Value)
        {
            throw new BusinessRuleException(
                "MaturityFromUtc cannot be later than MaturityToUtc.");
        }

        if (query.CounterpartyId.HasValue &&
            query.CounterpartyId.Value == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Counterparty ID is invalid.");
        }

        var result =
            await _placementRepository.Search(query);

        return new PagedInvestmentPlacementResponseDto
        {
            Items =
                result.Items
                    .Select(Map)
                    .ToList(),

            Page =
                query.Page,

            PageSize =
                query.PageSize,

            TotalCount =
                result.TotalCount,

            TotalPages =
                result.TotalCount == 0
                    ? 0
                    : (int)Math.Ceiling(
                        result.TotalCount /
                        (double)query.PageSize)
        };
    }

    public async Task<InvestmentPlacementResponseDto>
        Activate(
            Guid id,
            string idempotencyKey)
    {
        var normalizedKey =
            NormalizeIdempotencyKey(idempotencyKey);

        await _accountRepository.BeginTransaction();

        try
        {
            var placement =
                await GetPlacement(id);

            /*
            * Retrying an already submitted or completed
            * request must not reserve or debit funds again.
            */
            if ((placement.Status ==
                    InvestmentPlacementStatuses.Active ||
                placement.Status ==
                    InvestmentPlacementStatuses
                        .PendingActivation) &&
                string.Equals(
                    placement.ActivationIdempotencyKey,
                    normalizedKey,
                    StringComparison.Ordinal))
            {
                await _accountRepository
                    .CommitTransaction();

                return Map(placement);
            }

            if (placement.Status !=
                InvestmentPlacementStatuses.Draft)
            {
                throw new ConflictException(
                    "Only a draft investment placement " +
                    "can be submitted for activation.");
            }

            var account =
                GetFundingAccount(placement);

            ValidateFundingAccount(
                placement,
                account,
                fundsAlreadyReserved: false);

            var existingTransaction =
                await _transactionRepository
                    .GetByIdempotencyKey(normalizedKey);

            if (existingTransaction is not null)
            {
                throw new ConflictException(
                    "The idempotency key has already been used.");
            }

            await _limitEnforcementService
                .EnsureWithinLimits(
                    placement.CounterpartyId
                    ?? throw new BusinessRuleException(
                        "Assign a counterparty to the investment " +
                        "before requesting activation."),
                    placement.Currency,
                    placement.InvestmentType,
                    placement.PrincipalAmount,
                    placement.Id);

            var requirements =
                await _approvalPolicyService
                    .GetRequirements(
                        ApprovalOperationTypes
                            .InvestmentPlacement,
                        placement.Currency);

            var beforeValues =
                Snapshot(placement);

            var requestedAtUtc =
                DateTime.UtcNow;

            placement.ActivationIdempotencyKey =
                normalizedKey;

            placement.ActivationRequestedByUserId =
                _currentUserService.UserId;

            placement.ActivationRequestedAtUtc =
                requestedAtUtc;

            placement.UpdatedAtUtc =
                requestedAtUtc;

            placement.ConcurrencyToken =
                Guid.NewGuid();

            if (placement.PrincipalAmount >
                requirements.ThresholdAmount)
            {
                ReserveFunds(
                    account,
                    placement.PrincipalAmount);

                placement.Status =
                    InvestmentPlacementStatuses
                        .PendingActivation;

                placement.RequiredApprovalCount =
                    requirements.RequiredApprovalCount;

                placement.ApprovalCount = 0;

                placement.ActivationExpiresAtUtc =
                    requestedAtUtc.AddHours(
                        requirements
                            .PendingRequestExpiryHours);

                _placementRepository.Update(placement);

                await _accountRepository.SaveChanges();

                await _auditLogService.Record(
                    new CreateAuditLogDto
                    {
                        Action =
                            AuditActionTypes.Updated,

                        EntityType =
                            AuditEntityTypes
                                .InvestmentPlacement,

                        EntityId =
                            placement.Id,

                        EntityReference =
                            placement.Reference,

                        Summary =
                            $"Investment placement " +
                            $"{placement.Reference} was " +
                            $"submitted for approval.",

                        BeforeValues =
                            beforeValues,

                        AfterValues =
                            Snapshot(placement),

                        Metadata =
                            new
                            {
                                Module =
                                    "Investment Approvals",

                                placement.ApprovalCount,

                                placement
                                    .RequiredApprovalCount,

                                placement
                                    .ActivationExpiresAtUtc
                            }
                    });

                await _accountRepository
                    .CommitTransaction();

                return Map(placement);
            }

            placement.RequiredApprovalCount = 0;

            placement.ApprovalCount = 0;

            placement.ActivationExpiresAtUtc = null;

            var transaction =
                await ExecuteFunding(
                    placement,
                    account,
                    normalizedKey,
                    initiatedByUserId:
                        _currentUserService.UserId,
                    completedByUserId:
                        _currentUserService.UserId,
                    releaseReservation: false);

            await _accountRepository.SaveChanges();

            await RecordActivationApprovedAudit(
                beforeValues,
                placement,
                transaction,
                isFinalApproval: true);

            await _accountRepository
                .CommitTransaction();

            return Map(placement);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "The account or investment placement " +
                "changed while activation was processing.");
        }
        catch (DbUpdateException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "Investment activation could not be saved. " +
                "The idempotency key may already be in use.");
        }
        catch
        {
            await _accountRepository
                .RollbackTransaction();

            throw;
        }
    }

    public async Task<InvestmentPlacementResponseDto>
        ApproveActivation(Guid id)
    {
        await _accountRepository.BeginTransaction();

        try
        {
            var placement =
                await GetPendingActivation(id);

            EnsureDifferentReviewer(
                placement.ActivationRequestedByUserId);

            var beforeValues =
                Snapshot(placement);

            var currentUserId =
                _currentUserService.UserId;

            var alreadyReviewed =
                await _approvalDecisionRepository
                    .HasInvestmentPlacementDecision(
                        placement.Id,
                        currentUserId);

            if (alreadyReviewed)
            {
                throw new ConflictException(
                    "You have already reviewed this " +
                    "investment activation.");
            }

            await _approvalDecisionRepository.Add(
                new ApprovalDecision
                {
                    Id =
                        Guid.NewGuid(),

                    InvestmentPlacementId =
                        placement.Id,

                    ApproverUserId =
                        currentUserId,

                    Decision =
                        ApprovalDecisionTypes.Approved,

                    CreatedAtUtc =
                        DateTime.UtcNow
                });

            placement.ApprovalCount += 1;

            placement.UpdatedAtUtc =
                DateTime.UtcNow;

            placement.ConcurrencyToken =
                Guid.NewGuid();

            if (placement.ApprovalCount <
                placement.RequiredApprovalCount)
            {
                _placementRepository.Update(placement);

                await _accountRepository.SaveChanges();

                await RecordActivationApprovedAudit(
                    beforeValues,
                    placement,
                    transaction: null,
                    isFinalApproval: false);

                await _accountRepository
                    .CommitTransaction();

                return Map(placement);
            }

            /*
            * Revalidate at final approval because limits or other
            * exposure may have changed while approval was pending.
            */
            await _limitEnforcementService
                .EnsureWithinLimits(
                    placement.CounterpartyId
                    ?? throw new BusinessRuleException(
                        "Assign a counterparty before final " +
                        "investment approval."),
                    placement.Currency,
                    placement.InvestmentType,
                    placement.PrincipalAmount,
                    placement.Id);

            var account =
                GetFundingAccount(placement);

            ValidateFundingAccount(
                placement,
                account,
                fundsAlreadyReserved: true);

            var transaction =
                await ExecuteFunding(
                    placement,
                    account,
                    placement.ActivationIdempotencyKey
                        ?? throw new ConflictException(
                            "Activation idempotency key is missing."),
                    initiatedByUserId:
                        placement
                            .ActivationRequestedByUserId
                        ?? placement.CreatedByUserId
                        ?? currentUserId,
                    completedByUserId:
                        currentUserId,
                    releaseReservation: true);

            await _accountRepository.SaveChanges();

            await RecordActivationApprovedAudit(
                beforeValues,
                placement,
                transaction,
                isFinalApproval: true);

            await _accountRepository
                .CommitTransaction();

            return Map(placement);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "The placement or reservation changed " +
                "while approval was processing.");
        }
        catch
        {
            await _accountRepository
                .RollbackTransaction();

            throw;
        }
    }

    public async Task<InvestmentPlacementResponseDto>
        RejectActivation(
            Guid id,
            string reason)
    {
        var rejectionReason =
            NormalizeRequiredText(
                reason,
                "Rejection reason",
                500);

        await _accountRepository.BeginTransaction();

        try
        {
            var placement =
                await GetPendingActivation(id);

            EnsureDifferentReviewer(
                placement.ActivationRequestedByUserId);

            var beforeValues =
                Snapshot(placement);

            var currentUserId =
                _currentUserService.UserId;

            var alreadyReviewed =
                await _approvalDecisionRepository
                    .HasInvestmentPlacementDecision(
                        placement.Id,
                        currentUserId);

            if (alreadyReviewed)
            {
                throw new ConflictException(
                    "You have already reviewed this " +
                    "investment activation.");
            }

            await _approvalDecisionRepository.Add(
                new ApprovalDecision
                {
                    Id =
                        Guid.NewGuid(),

                    InvestmentPlacementId =
                        placement.Id,

                    ApproverUserId =
                        currentUserId,

                    Decision =
                        ApprovalDecisionTypes.Rejected,

                    Comment =
                        rejectionReason,

                    CreatedAtUtc =
                        DateTime.UtcNow
                });

            var account =
                GetFundingAccount(placement);

            ReleaseReservedFunds(
                account,
                placement.PrincipalAmount);

            placement.Status =
                InvestmentPlacementStatuses
                    .ActivationRejected;

            placement.ActivationRejectedByUserId =
                currentUserId;

            placement.ActivationRejectedAtUtc =
                DateTime.UtcNow;

            placement.ActivationRejectionReason =
                rejectionReason;

            placement.UpdatedAtUtc =
                DateTime.UtcNow;

            placement.ConcurrencyToken =
                Guid.NewGuid();

            _placementRepository.Update(placement);

            await _accountRepository.SaveChanges();

            await _auditLogService.Record(
                new CreateAuditLogDto
                {
                    Action =
                        AuditActionTypes.Rejected,

                    EntityType =
                        AuditEntityTypes
                            .InvestmentPlacement,

                    EntityId =
                        placement.Id,

                    EntityReference =
                        placement.Reference,

                    Summary =
                        $"Investment activation " +
                        $"{placement.Reference} was rejected.",

                    BeforeValues =
                        beforeValues,

                    AfterValues =
                        Snapshot(placement),

                    Metadata =
                        new
                        {
                            Module =
                                "Investment Approvals",

                            RejectionReason =
                                rejectionReason
                        }
                });

            await _accountRepository
                .CommitTransaction();

            return Map(placement);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "The placement or reservation changed " +
                "while rejection was processing.");
        }
        catch
        {
            await _accountRepository
                .RollbackTransaction();

            throw;
        }
    }

    public async Task<InvestmentPortfolioReportDto>
        GetPortfolioReport(
            InvestmentPortfolioQueryDto query)
    {
        var normalizedQuery =
            NormalizePortfolioQuery(query);

        var placements =
            await _placementRepository
                .GetForReporting(normalizedQuery);

        var generatedAtUtc =
            DateTime.UtcNow;

        var outstanding =
            placements
                .Where(placement =>
                    placement.Status ==
                        InvestmentPlacementStatuses.Active ||
                    placement.Status ==
                        InvestmentPlacementStatuses.Matured)
                .ToList();

        var redeemed =
            placements
                .Where(placement =>
                    placement.Status ==
                        InvestmentPlacementStatuses.Redeemed)
                .ToList();

        var outstandingPrincipal =
            outstanding.Sum(placement =>
                placement.PrincipalAmount);

        var buckets =
            placements
                .GroupBy(placement => new
                {
                    placement.Currency,
                    placement.InstitutionName
                })
                .Select(group =>
                {
                    var bucketOutstanding =
                        group
                            .Where(placement =>
                                placement.Status ==
                                    InvestmentPlacementStatuses.Active ||
                                placement.Status ==
                                    InvestmentPlacementStatuses.Matured)
                            .ToList();

                    var bucketRedeemed =
                        group
                            .Where(placement =>
                                placement.Status ==
                                    InvestmentPlacementStatuses.Redeemed)
                            .ToList();

                    var bucketPrincipal =
                        bucketOutstanding.Sum(placement =>
                            placement.PrincipalAmount);

                    return new InvestmentPortfolioBucketDto
                    {
                        Currency =
                            group.Key.Currency,

                        InstitutionName =
                            group.Key.InstitutionName,

                        PlacementCount =
                            group.Count(),

                        ActiveCount =
                            group.Count(placement =>
                                placement.Status ==
                                    InvestmentPlacementStatuses.Active),

                        MaturedCount =
                            group.Count(placement =>
                                placement.Status ==
                                    InvestmentPlacementStatuses.Matured),

                        RedeemedCount =
                            bucketRedeemed.Count,

                        OverdueUnredeemedCount =
                            bucketOutstanding.Count(placement =>
                                placement.MaturityDateUtc.Date <
                                    generatedAtUtc.Date),

                        OutstandingPrincipal =
                            bucketPrincipal,

                        OutstandingExpectedInterest =
                            bucketOutstanding.Sum(placement =>
                                placement.ExpectedInterestAmount),

                        OutstandingExpectedMaturityAmount =
                            bucketOutstanding.Sum(placement =>
                                placement.ExpectedMaturityAmount),

                        ActualRedeemedProceeds =
                            bucketRedeemed.Sum(placement =>
                                placement.ActualMaturityAmount),

                        WeightedAverageInterestRate =
                            CalculateWeightedAverageRate(
                                bucketOutstanding),

                        NextMaturityDateUtc =
                            GetNextMaturityDate(
                                bucketOutstanding,
                                generatedAtUtc)
                    };
                })
                .OrderBy(bucket =>
                    bucket.Currency)
                .ThenBy(bucket =>
                    bucket.InstitutionName)
                .ToList();

        return new InvestmentPortfolioReportDto
        {
            GeneratedAtUtc =
                generatedAtUtc,

            CurrencyFilter =
                normalizedQuery.Currency,

            InstitutionFilter =
                normalizedQuery.InstitutionName,

            MaturityFromUtc =
                normalizedQuery.MaturityFromUtc,

            MaturityToUtc =
                normalizedQuery.MaturityToUtc,

            IncludesRedeemed =
                normalizedQuery.IncludeRedeemed,

            PlacementCount =
                placements.Count,

            ActiveCount =
                placements.Count(placement =>
                    placement.Status ==
                        InvestmentPlacementStatuses.Active),

            MaturedCount =
                placements.Count(placement =>
                    placement.Status ==
                        InvestmentPlacementStatuses.Matured),

            RedeemedCount =
                redeemed.Count,

            OverdueUnredeemedCount =
                outstanding.Count(placement =>
                    placement.MaturityDateUtc.Date <
                        generatedAtUtc.Date),

            OutstandingPrincipal =
                outstandingPrincipal,

            OutstandingExpectedInterest =
                outstanding.Sum(placement =>
                    placement.ExpectedInterestAmount),

            OutstandingExpectedMaturityAmount =
                outstanding.Sum(placement =>
                    placement.ExpectedMaturityAmount),

            RedeemedPrincipal =
                redeemed.Sum(placement =>
                    placement.PrincipalAmount),

            ActualInterestEarned =
                redeemed.Sum(placement =>
                    placement.ActualInterestAmount),

            WithholdingTaxAmount =
                redeemed.Sum(placement =>
                    placement.WithholdingTaxAmount),

            ActualRedeemedProceeds =
                redeemed.Sum(placement =>
                    placement.ActualMaturityAmount),

            WeightedAverageInterestRate =
                CalculateWeightedAverageRate(
                    outstanding),

            NextMaturityDateUtc =
                GetNextMaturityDate(
                    outstanding,
                    generatedAtUtc),

            Buckets =
                buckets
        };
    }

    public async Task<InvestmentMaturityScheduleDto>
        GetMaturitySchedule(
            InvestmentPortfolioQueryDto query)
    {
        var normalizedQuery =
            NormalizePortfolioQuery(query);

        var placements =
            await _placementRepository
                .GetForReporting(normalizedQuery);

        var generatedAtUtc =
            DateTime.UtcNow;

        var items =
            placements
                .OrderBy(placement =>
                    placement.MaturityDateUtc)
                .Select(placement =>
                    new InvestmentMaturityScheduleItemDto
                    {
                        PlacementId =
                            placement.Id,

                        Reference =
                            placement.Reference,

                        InstitutionName =
                            placement.InstitutionName,

                        Currency =
                            placement.Currency,

                        PrincipalAmount =
                            placement.PrincipalAmount,

                        AnnualInterestRate =
                            placement.AnnualInterestRate,

                        ExpectedInterestAmount =
                            placement.ExpectedInterestAmount,

                        ExpectedMaturityAmount =
                            placement.ExpectedMaturityAmount,

                        StartDateUtc =
                            placement.StartDateUtc,

                        MaturityDateUtc =
                            placement.MaturityDateUtc,

                        DaysToMaturity =
                            (placement.MaturityDateUtc.Date -
                            generatedAtUtc.Date).Days,

                        Status =
                            placement.Status,

                        IsOverdue =
                            placement.Status !=
                                InvestmentPlacementStatuses.Redeemed &&
                            placement.MaturityDateUtc.Date <
                                generatedAtUtc.Date,

                        ActualMaturityAmount =
                            placement.ActualMaturityAmount,

                        RedeemedAtUtc =
                            placement.RedeemedAtUtc
                    })
                .ToList();

        return new InvestmentMaturityScheduleDto
        {
            GeneratedAtUtc =
                generatedAtUtc,

            PlacementCount =
                items.Count,

            OverdueCount =
                items.Count(item =>
                    item.IsOverdue),

            TotalPrincipalAmount =
                items.Sum(item =>
                    item.PrincipalAmount),

            TotalExpectedMaturityAmount =
                items.Sum(item =>
                    item.ExpectedMaturityAmount),

            Items =
                items
        };
    }

    public async Task<CsvExportDto>
        ExportPortfolioCsv(
            InvestmentPortfolioQueryDto query)
    {
        var report =
            await GetPortfolioReport(query);

        var schedule =
            await GetMaturitySchedule(query);

        var csv =
            new StringBuilder();

        // Section 1: overall portfolio summary.
        csv.AppendLine(
            "ReportType,GeneratedAtUtc,PlacementCount,ActiveCount,MaturedCount,RedeemedCount,OverdueUnredeemedCount,OutstandingPrincipal,OutstandingExpectedInterest,OutstandingExpectedMaturityAmount,RedeemedPrincipal,ActualInterestEarned,WithholdingTaxAmount,ActualRedeemedProceeds,WeightedAverageInterestRate,NextMaturityDateUtc");

        csv.AppendLine(string.Join(
            ",",
            CsvExportHelper.Escape(
                "InvestmentPortfolioSummary"),
            CsvExportHelper.Escape(
                report.GeneratedAtUtc),
            CsvExportHelper.Escape(
                report.PlacementCount),
            CsvExportHelper.Escape(
                report.ActiveCount),
            CsvExportHelper.Escape(
                report.MaturedCount),
            CsvExportHelper.Escape(
                report.RedeemedCount),
            CsvExportHelper.Escape(
                report.OverdueUnredeemedCount),
            CsvExportHelper.Escape(
                report.OutstandingPrincipal),
            CsvExportHelper.Escape(
                report.OutstandingExpectedInterest),
            CsvExportHelper.Escape(
                report.OutstandingExpectedMaturityAmount),
            CsvExportHelper.Escape(
                report.RedeemedPrincipal),
            CsvExportHelper.Escape(
                report.ActualInterestEarned),
            CsvExportHelper.Escape(
                report.WithholdingTaxAmount),
            CsvExportHelper.Escape(
                report.ActualRedeemedProceeds),
            CsvExportHelper.Escape(
                report.WeightedAverageInterestRate),
            CsvExportHelper.Escape(
                report.NextMaturityDateUtc)));

        csv.AppendLine();

        // Section 2: exposure by institution and currency.
        csv.AppendLine(
            "Currency,InstitutionName,PlacementCount,ActiveCount,MaturedCount,RedeemedCount,OverdueUnredeemedCount,OutstandingPrincipal,OutstandingExpectedInterest,OutstandingExpectedMaturityAmount,ActualRedeemedProceeds,WeightedAverageInterestRate,NextMaturityDateUtc");

        foreach (var bucket in report.Buckets)
        {
            csv.AppendLine(string.Join(
                ",",
                CsvExportHelper.Escape(
                    bucket.Currency),
                CsvExportHelper.Escape(
                    bucket.InstitutionName),
                CsvExportHelper.Escape(
                    bucket.PlacementCount),
                CsvExportHelper.Escape(
                    bucket.ActiveCount),
                CsvExportHelper.Escape(
                    bucket.MaturedCount),
                CsvExportHelper.Escape(
                    bucket.RedeemedCount),
                CsvExportHelper.Escape(
                    bucket.OverdueUnredeemedCount),
                CsvExportHelper.Escape(
                    bucket.OutstandingPrincipal),
                CsvExportHelper.Escape(
                    bucket.OutstandingExpectedInterest),
                CsvExportHelper.Escape(
                    bucket.OutstandingExpectedMaturityAmount),
                CsvExportHelper.Escape(
                    bucket.ActualRedeemedProceeds),
                CsvExportHelper.Escape(
                    bucket.WeightedAverageInterestRate),
                CsvExportHelper.Escape(
                    bucket.NextMaturityDateUtc)));
        }

        csv.AppendLine();

        // Section 3: placement-level maturity schedule.
        csv.AppendLine(
            "PlacementId,Reference,InstitutionName,Currency,PrincipalAmount,AnnualInterestRate,ExpectedInterestAmount,ExpectedMaturityAmount,StartDateUtc,MaturityDateUtc,DaysToMaturity,Status,IsOverdue,ActualMaturityAmount,RedeemedAtUtc");

        foreach (var item in schedule.Items)
        {
            csv.AppendLine(string.Join(
                ",",
                CsvExportHelper.Escape(
                    item.PlacementId),
                CsvExportHelper.Escape(
                    item.Reference),
                CsvExportHelper.Escape(
                    item.InstitutionName),
                CsvExportHelper.Escape(
                    item.Currency),
                CsvExportHelper.Escape(
                    item.PrincipalAmount),
                CsvExportHelper.Escape(
                    item.AnnualInterestRate),
                CsvExportHelper.Escape(
                    item.ExpectedInterestAmount),
                CsvExportHelper.Escape(
                    item.ExpectedMaturityAmount),
                CsvExportHelper.Escape(
                    item.StartDateUtc),
                CsvExportHelper.Escape(
                    item.MaturityDateUtc),
                CsvExportHelper.Escape(
                    item.DaysToMaturity),
                CsvExportHelper.Escape(
                    item.Status),
                CsvExportHelper.Escape(
                    item.IsOverdue),
                CsvExportHelper.Escape(
                    item.ActualMaturityAmount),
                CsvExportHelper.Escape(
                    item.RedeemedAtUtc)));
        }

        var timestamp =
            DateTime.UtcNow.ToString(
                "yyyyMMddHHmmss");

        return new CsvExportDto
        {
            FileName =
                $"investment-portfolio-{timestamp}.csv",

            ContentType =
                "text/csv",

            Content =
                CsvExportHelper.ToUtf8Bytes(
                    csv.ToString())
        };
    }

    public async Task<InvestmentMaturityProcessingResultDto>
        ProcessDueMaturities(
            int maxRows = 100)
    {
        var normalizedMaxRows =
            maxRows < 1
                ? 100
                : Math.Min(maxRows, 500);

        var processedAtUtc =
            DateTime.UtcNow;

        await _accountRepository.BeginTransaction();

        try
        {
            var placements =
                await _placementRepository
                    .GetDueForMaturity(
                        processedAtUtc,
                        normalizedMaxRows);

            var beforeValues =
                placements.ToDictionary(
                    placement => placement.Id,
                    placement => Snapshot(placement));

            foreach (var placement in placements)
            {
                placement.Status =
                    InvestmentPlacementStatuses.Matured;

                placement.UpdatedAtUtc =
                    processedAtUtc;

                placement.ConcurrencyToken =
                    Guid.NewGuid();

                _placementRepository.Update(placement);
            }

            await _accountRepository.SaveChanges();

            foreach (var placement in placements)
            {
                await _auditLogService.Record(
                    new CreateAuditLogDto
                    {
                        Action =
                            AuditActionTypes.Matured,

                        EntityType =
                            AuditEntityTypes
                                .InvestmentPlacement,

                        EntityId =
                            placement.Id,

                        EntityReference =
                            placement.Reference,

                        Summary =
                            $"Investment placement " +
                            $"{placement.Reference} matured.",

                        BeforeValues =
                            beforeValues[placement.Id],

                        AfterValues =
                            Snapshot(placement),

                        Metadata =
                            new
                            {
                                Module =
                                    "Investment Placements",

                                placement.MaturityDateUtc,

                                placement
                                    .ExpectedMaturityAmount
                            }
                    });
            }

            await _accountRepository.CommitTransaction();

            return new InvestmentMaturityProcessingResultDto
            {
                ProcessedAtUtc =
                    processedAtUtc,

                MaturedCount =
                    placements.Count,

                PlacementIds =
                    placements
                        .Select(placement =>
                            placement.Id)
                        .ToList()
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository.RollbackTransaction();

            throw new ConflictException(
                "An investment placement changed while " +
                "maturities were being processed.");
        }
        catch
        {
            await _accountRepository.RollbackTransaction();

            throw;
        }
    }

    public async Task<InvestmentPlacementResponseDto>
        Redeem(
            Guid id,
            RedeemInvestmentPlacementDto dto)
    {
        ValidateRedemption(dto);

        var idempotencyKey =
            NormalizeIdempotencyKey(
                dto.IdempotencyKey);

        await _accountRepository.BeginTransaction();

        try
        {
            var placement =
                await GetPlacement(id);

            /*
            * A retry with the same key returns the original
            * result without crediting the account again.
            */
            if (placement.Status ==
                    InvestmentPlacementStatuses.Redeemed &&
                string.Equals(
                    placement.RedemptionIdempotencyKey,
                    idempotencyKey,
                    StringComparison.Ordinal))
            {
                await _accountRepository.CommitTransaction();

                return Map(placement);
            }

            /*
            * This permits redemption even if the maturity
            * processing job has not run yet.
            */
            if (placement.Status ==
                    InvestmentPlacementStatuses.Active &&
                placement.MaturityDateUtc <=
                    DateTime.UtcNow)
            {
                placement.Status =
                    InvestmentPlacementStatuses.Matured;
            }

            if (placement.Status !=
                InvestmentPlacementStatuses.Matured)
            {
                throw new ConflictException(
                    "Only a matured investment placement " +
                    "can be redeemed.");
            }

            var existingTransaction =
                await _transactionRepository
                    .GetByIdempotencyKey(
                        idempotencyKey);

            if (existingTransaction is not null)
            {
                throw new ConflictException(
                    "The redemption idempotency key has " +
                    "already been used.");
            }

            var destinationAccount =
                await _accountRepository.GetById(
                    dto.DestinationAccountId);

            if (destinationAccount is null)
            {
                throw new ResourceNotFoundException(
                    "Redemption destination account " +
                    "was not found.");
            }

            if (!destinationAccount.IsActive)
            {
                throw new ForbiddenOperationException(
                    "Redemption requires an active " +
                    "destination account.");
            }

            if (!string.Equals(
                    destinationAccount.Currency,
                    placement.Currency,
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

            var actualMaturityAmount =
                placement.PrincipalAmount +
                dto.ActualInterestAmount -
                dto.WithholdingTaxAmount;

            var beforeValues =
                Snapshot(placement);

            var redeemedAtUtc =
                DateTime.UtcNow;

            var externalReference =
                NormalizeOptionalText(
                    dto.ExternalReference,
                    100);

            var notes =
                NormalizeOptionalText(
                    dto.Notes,
                    1000);

            var description =
                $"Redemption proceeds for investment " +
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
                        actualMaturityAmount,

                    Currency =
                        placement.Currency,

                    Description =
                        description,

                    SourceAccountId =
                        null,

                    DestinationAccountId =
                        destinationAccount.Id,

                    Category =
                        "Investment Redemption",

                    CounterpartyName =
                        placement.InstitutionName,

                    ExternalReference =
                        externalReference ??
                        placement.Reference,

                    IdempotencyKey =
                        idempotencyKey,

                    InitiatedByUserId =
                        _currentUserService.UserId,

                    CompletedByUserId =
                        _currentUserService.UserId,

                    CreatedAtUtc =
                        redeemedAtUtc,

                    CompletedAtUtc =
                        redeemedAtUtc
                };

            destinationAccount.Balance +=
                actualMaturityAmount;

            destinationAccount.ConcurrencyToken =
                Guid.NewGuid();

            /*
            * Cash entering the bank account increases the
            * bank-account asset and is recorded as a debit.
            */
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
                        actualMaturityAmount,

                    EntryType =
                        "Debit",

                    Description =
                        description,

                    CreatedAt =
                        redeemedAtUtc
                };

            maturityForecast.Status =
                CashFlowForecastStatus.Realized;

            maturityForecast.RealizedTreasuryTransactionId =
                transaction.Id;

            maturityForecast.RealizedTreasuryTransaction =
                transaction;

            maturityForecast.RealizedAtUtc =
                redeemedAtUtc;

            maturityForecast.UpdatedAtUtc =
                redeemedAtUtc;

            maturityForecast.ConcurrencyToken =
                Guid.NewGuid();

            placement.Status =
                InvestmentPlacementStatuses.Redeemed;

            placement.RedemptionIdempotencyKey =
                idempotencyKey;

            placement.RedemptionAccountId =
                destinationAccount.Id;

            placement.RedemptionAccount =
                destinationAccount;

            placement.RedemptionTreasuryTransactionId =
                transaction.Id;

            placement.RedemptionTreasuryTransaction =
                transaction;

            placement.ActualInterestAmount =
                dto.ActualInterestAmount;

            placement.WithholdingTaxAmount =
                dto.WithholdingTaxAmount;

            placement.ActualMaturityAmount =
                actualMaturityAmount;

            placement.RedemptionExternalReference =
                externalReference;

            placement.RedemptionNotes =
                notes;

            placement.RedeemedByUserId =
                _currentUserService.UserId;

            placement.RedeemedAtUtc =
                redeemedAtUtc;

            placement.UpdatedAtUtc =
                redeemedAtUtc;

            placement.ConcurrencyToken =
                Guid.NewGuid();

            _accountRepository.Update(
                destinationAccount);

            _forecastRepository.Update(
                maturityForecast);

            _placementRepository.Update(
                placement);

            await _transactionRepository.Add(
                transaction);

            await _ledgerRepository.Add(
                ledgerEntry);

            await _accountRepository.SaveChanges();

            await _auditLogService.Record(
                new CreateAuditLogDto
                {
                    Action =
                        AuditActionTypes.Redeemed,

                    EntityType =
                        AuditEntityTypes
                            .InvestmentPlacement,

                    EntityId =
                        placement.Id,

                    EntityReference =
                        placement.Reference,

                    Summary =
                        $"Investment placement " +
                        $"{placement.Reference} was redeemed.",

                    BeforeValues =
                        beforeValues,

                    AfterValues =
                        Snapshot(placement),

                    Metadata =
                        new
                        {
                            Module =
                                "Investment Placements",

                            RedemptionTransactionId =
                                transaction.Id,

                            RedemptionTransactionReference =
                                transaction.Reference,

                            DestinationAccountId =
                                destinationAccount.Id,

                            placement.PrincipalAmount,

                            placement.ActualInterestAmount,

                            placement.WithholdingTaxAmount,

                            placement.ActualMaturityAmount
                        }
                });

            await _accountRepository.CommitTransaction();

            return Map(placement);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository.RollbackTransaction();

            throw new ConflictException(
                "The placement, forecast or destination " +
                "account changed during redemption.");
        }
        catch (DbUpdateException)
        {
            await _accountRepository.RollbackTransaction();

            throw new ConflictException(
                "The investment redemption could not be " +
                "saved. It may already have been processed.");
        }
        catch
        {
            await _accountRepository.RollbackTransaction();

            throw;
        }
    }

    public async Task<InvestmentPlacementResponseDto>
        Cancel(
        Guid id,
        string reason)
    {
        var placement =
            await _placementRepository.GetById(id);

        if (placement is null)
        {
            throw new ResourceNotFoundException(
                "Investment placement was not found.");
        }

        if (placement.Status !=
            InvestmentPlacementStatuses.Draft)
        {
            throw new ConflictException(
                "Only a draft investment placement can be cancelled.");
        }

        var cancellationReason =
            NormalizeRequiredText(
                reason,
                "Cancellation reason",
                500);

        var beforeValues =
            Snapshot(placement);

        placement.Status =
            InvestmentPlacementStatuses.Cancelled;

        placement.CancellationReason =
            cancellationReason;

        placement.CancelledByUserId =
            _currentUserService.UserId;

        placement.CancelledAtUtc =
            DateTime.UtcNow;

        placement.UpdatedAtUtc =
            DateTime.UtcNow;

        placement.ConcurrencyToken =
            Guid.NewGuid();

        _placementRepository.Update(placement);

        await _placementRepository.SaveChanges();

        var result =
            Map(placement);

        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Cancelled,

                EntityType =
                    AuditEntityTypes.InvestmentPlacement,

                EntityId =
                    placement.Id,

                EntityReference =
                    placement.Reference,

                Summary =
                    $"Investment placement {placement.Reference} was cancelled.",

                BeforeValues =
                    beforeValues,

                AfterValues =
                    Snapshot(placement),

                Metadata =
                    new
                    {
                        Module = "Investment Placements",
                        Reason = cancellationReason
                    }
            });

        return result;
    }

    private async Task<TreasuryTransaction>
        ExecuteFunding(
            InvestmentPlacement placement,
            Account account,
            string idempotencyKey,
            Guid initiatedByUserId,
            Guid completedByUserId,
            bool releaseReservation)
    {
        ValidateFundingAccount(
            placement,
            account,
            releaseReservation);

        var existingTransaction =
            await _transactionRepository
                .GetByIdempotencyKey(idempotencyKey);

        if (existingTransaction is not null)
        {
            throw new ConflictException(
                "The activation has already been funded.");
        }

        var activatedAtUtc =
            DateTime.UtcNow;

        var description =
            $"Funding for investment placement " +
            $"{placement.Reference} with " +
            $"{placement.InstitutionName}.";

        var transaction =
            new TreasuryTransaction
            {
                Id =
                    Guid.NewGuid(),

                Reference =
                    TransactionReferenceGenerator.Generate(),

                TransactionType =
                    TransactionTypes.InvestmentPlacement,

                Status =
                    TransactionStatuses.Completed,

                Amount =
                    placement.PrincipalAmount,

                Currency =
                    placement.Currency,

                Description =
                    description,

                SourceAccountId =
                    account.Id,

                Category =
                    "Investment Placement",

                CounterpartyName =
                    placement.InstitutionName,

                ExternalReference =
                    placement.Reference,

                IdempotencyKey =
                    idempotencyKey,

                InitiatedByUserId =
                    initiatedByUserId,

                CompletedByUserId =
                    completedByUserId,

                CreatedAtUtc =
                    activatedAtUtc,

                CompletedAtUtc =
                    activatedAtUtc
            };

        var maturityForecast =
            new CashFlowForecastItem
            {
                Id =
                    Guid.NewGuid(),

                AccountId =
                    account.Id,

                Direction =
                    CashFlowDirections.Inflow,

                Amount =
                    placement.ExpectedMaturityAmount,

                Currency =
                    placement.Currency,

                ExpectedDateUtc =
                    placement.MaturityDateUtc,

                Category =
                    "Investment Maturity",

                CounterpartyName =
                    placement.InstitutionName,

                Description =
                    $"Expected maturity proceeds for " +
                    $"{placement.Reference}.",

                SourceType =
                    CashFlowForecastSourceTypes.Investment,

                Status =
                    CashFlowForecastStatus.Active,

                CreatedByUserId =
                    initiatedByUserId,

                CreatedAtUtc =
                    activatedAtUtc,

                UpdatedAtUtc =
                    activatedAtUtc,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        if (releaseReservation)
        {
            ReleaseReservedFunds(
                account,
                placement.PrincipalAmount);
        }

        account.Balance -=
            placement.PrincipalAmount;

        if (account.ReservedBalance >
            account.Balance)
        {
            throw new ConflictException(
                "Remaining reservations exceed the " +
                "account balance after funding.");
        }

        account.ConcurrencyToken =
            Guid.NewGuid();

        placement.Status =
            InvestmentPlacementStatuses.Active;

        placement.FundingTreasuryTransactionId =
            transaction.Id;

        placement.FundingTreasuryTransaction =
            transaction;

        placement.MaturityForecastItemId =
            maturityForecast.Id;

        placement.MaturityForecastItem =
            maturityForecast;

        placement.ActivatedByUserId =
            completedByUserId;

        placement.ActivatedAtUtc =
            activatedAtUtc;

        placement.UpdatedAtUtc =
            activatedAtUtc;

        placement.ConcurrencyToken =
            Guid.NewGuid();

        _accountRepository.Update(account);

        _placementRepository.Update(placement);

        await _transactionRepository.Add(transaction);

        await _ledgerRepository.Add(
            new LedgerEntry
            {
                Id =
                    Guid.NewGuid(),

                AccountId =
                    account.Id,

                TreasuryTransactionId =
                    transaction.Id,

                Amount =
                    placement.PrincipalAmount,

                EntryType =
                    "Credit",

                Description =
                    description,

                CreatedAt =
                    activatedAtUtc
            });

        await _forecastRepository.Add(
            maturityForecast);

        return transaction;
    }

    private static void ValidateCreateRequest(
        CreateInvestmentPlacementDto dto)
    {
        if (dto.SourceAccountId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Source account is required.");
        }

        if (dto.CounterpartyId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Investment counterparty is required.");
        }

        if (dto.PrincipalAmount <= 0)
        {
            throw new BusinessRuleException(
                "Principal amount must be greater than zero.");
        }

        if (dto.AnnualInterestRate < 0 ||
            dto.AnnualInterestRate > 100)
        {
            throw new BusinessRuleException(
                "Annual interest rate must be between 0 and 100.");
        }

        if (dto.DayCountBasis != 360 &&
            dto.DayCountBasis != 365)
        {
            throw new BusinessRuleException(
                "Day-count basis must be either 360 or 365.");
        }
    }

    private async Task<string> GenerateReference()
    {
        for (var attempt = 0;
             attempt < 10;
             attempt++)
        {
            var reference =
                $"INV-{DateTime.UtcNow:yyyyMMdd}-" +
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
            "Unable to generate a unique investment reference.");
    }

    private async Task<InvestmentPlacement>
        GetPlacement(Guid id)
    {
        var placement =
            await _placementRepository.GetById(id);

        if (placement is null)
        {
            throw new ResourceNotFoundException(
                "Investment placement was not found.");
        }

        return placement;
    }

    private async Task<InvestmentPlacement>
        GetPendingActivation(Guid id)
    {
        var placement =
            await GetPlacement(id);

        if (placement.Status !=
            InvestmentPlacementStatuses.PendingActivation)
        {
            throw new ConflictException(
                "The investment placement is not " +
                "awaiting activation approval.");
        }

        PendingRequestExpiryGuard.EnsureNotExpired(
            placement.ActivationExpiresAtUtc,
            "investment activation");

        return placement;
    }

    private static Account GetFundingAccount(
        InvestmentPlacement placement)
    {
        return placement.SourceAccount
            ?? throw new ResourceNotFoundException(
                "Investment source account was not loaded.");
    }

    private static void ValidateFundingAccount(
        InvestmentPlacement placement,
        Account account,
        bool fundsAlreadyReserved)
    {
        if (!account.IsActive)
        {
            throw new ForbiddenOperationException(
                "Investment funding requires an active account.");
        }

        if (placement.MaturityDateUtc.Date <=
            DateTime.UtcNow.Date)
        {
            throw new BusinessRuleException(
                "The placement has reached its maturity date.");
        }

        if (fundsAlreadyReserved &&
            account.ReservedBalance <
                placement.PrincipalAmount)
        {
            throw new ConflictException(
                "The expected investment reservation " +
                "was not found.");
        }

        var spendableBalance =
            account.AvailableBalance +
            (fundsAlreadyReserved
                ? placement.PrincipalAmount
                : 0);

        if (spendableBalance <
            placement.PrincipalAmount)
        {
            throw new BusinessRuleException(
                "Insufficient available funds.");
        }
    }

    private void EnsureDifferentReviewer(
        Guid? requestedByUserId)
    {
        if (requestedByUserId.HasValue &&
            requestedByUserId.Value ==
                _currentUserService.UserId)
        {
            throw new ForbiddenOperationException(
                "You cannot approve or reject your " +
                "own investment activation.");
        }
    }

    private void ReserveFunds(
        Account account,
        decimal amount)
    {
        if (account.AvailableBalance < amount)
        {
            throw new BusinessRuleException(
                "Insufficient available funds.");
        }

        account.ReservedBalance += amount;

        account.ConcurrencyToken =
            Guid.NewGuid();

        _accountRepository.Update(account);
    }

    private void ReleaseReservedFunds(
        Account account,
        decimal amount)
    {
        if (account.ReservedBalance < amount)
        {
            throw new ConflictException(
                "The expected investment reservation " +
                "was not found.");
        }

        account.ReservedBalance -= amount;

        account.ConcurrencyToken =
            Guid.NewGuid();

        _accountRepository.Update(account);
    }

    private async Task RecordActivationApprovedAudit(
        object beforeValues,
        InvestmentPlacement placement,
        TreasuryTransaction? transaction,
        bool isFinalApproval)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    isFinalApproval
                        ? AuditActionTypes.Activated
                        : AuditActionTypes.Approved,

                EntityType =
                    AuditEntityTypes.InvestmentPlacement,

                EntityId =
                    placement.Id,

                EntityReference =
                    placement.Reference,

                Summary =
                    isFinalApproval
                        ? $"Investment placement " +
                        $"{placement.Reference} received " +
                        $"final approval and was funded."
                        : $"Investment placement " +
                        $"{placement.Reference} received " +
                        $"partial approval.",

                BeforeValues =
                    beforeValues,

                AfterValues =
                    Snapshot(placement),

                Metadata =
                    new
                    {
                        Module =
                            "Investment Approvals",

                        IsFinalApproval =
                            isFinalApproval,

                        placement.ApprovalCount,

                        placement.RequiredApprovalCount,

                        TransactionId =
                            transaction?.Id,

                        TransactionReference =
                            transaction?.Reference
                    }
            });
    }

    private static void ValidateRedemption(
        RedeemInvestmentPlacementDto dto)
    {
        if (dto.DestinationAccountId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Destination account is required.");
        }

        if (dto.ActualInterestAmount < 0)
        {
            throw new BusinessRuleException(
                "Actual interest amount cannot be negative.");
        }

        if (dto.WithholdingTaxAmount < 0)
        {
            throw new BusinessRuleException(
                "Withholding tax amount cannot be negative.");
        }

        if (dto.WithholdingTaxAmount >
            dto.ActualInterestAmount)
        {
            throw new BusinessRuleException(
                "Withholding tax cannot exceed the " +
                "actual interest amount.");
        }
    }

    private static InvestmentPortfolioQueryDto
        NormalizePortfolioQuery(
            InvestmentPortfolioQueryDto query)
    {
        DateTime? maturityFromUtc =
            query.MaturityFromUtc.HasValue
                ? NormalizeUtc(
                    query.MaturityFromUtc.Value).Date
                : null;

        DateTime? maturityToUtc =
            query.MaturityToUtc.HasValue
                ? NormalizeUtc(
                        query.MaturityToUtc.Value)
                    .Date
                    .AddDays(1)
                    .AddTicks(-1)
                : null;

        if (maturityFromUtc.HasValue &&
            maturityToUtc.HasValue &&
            maturityFromUtc.Value >
                maturityToUtc.Value)
        {
            throw new BusinessRuleException(
                "MaturityFromUtc cannot be later " +
                "than MaturityToUtc.");
        }

        if (query.CounterpartyId.HasValue &&
            query.CounterpartyId.Value == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Counterparty ID is invalid.");
        }

        return new InvestmentPortfolioQueryDto
        {
            Currency =
                string.IsNullOrWhiteSpace(
                    query.Currency)
                    ? null
                    : NormalizeCurrency(
                        query.Currency),

            InstitutionName =
                NormalizeOptionalText(
                    query.InstitutionName,
                    200),
            
            CounterpartyId =
                query.CounterpartyId,

            MaturityFromUtc =
                maturityFromUtc,

            MaturityToUtc =
                maturityToUtc,

            IncludeRedeemed =
                query.IncludeRedeemed
        };
    }

    private static decimal
        CalculateWeightedAverageRate(
            IEnumerable<InvestmentPlacement> placements)
    {
        var items =
            placements.ToList();

        var totalPrincipal =
            items.Sum(placement =>
                placement.PrincipalAmount);

        if (totalPrincipal <= 0)
        {
            return 0;
        }

        var weightedRate =
            items.Sum(placement =>
                placement.PrincipalAmount *
                placement.AnnualInterestRate) /
            totalPrincipal;

        return Math.Round(
            weightedRate,
            6,
            MidpointRounding.AwayFromZero);
    }

    private static DateTime? GetNextMaturityDate(
        IEnumerable<InvestmentPlacement> placements,
        DateTime asOfUtc)
    {
        return placements
            .Where(placement =>
                placement.MaturityDateUtc.Date >=
                    asOfUtc.Date)
            .OrderBy(placement =>
                placement.MaturityDateUtc)
            .Select(placement =>
                (DateTime?)placement.MaturityDateUtc)
            .FirstOrDefault();
    }

    private static string NormalizeIdempotencyKey(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                "Idempotency key is required.");
        }

        var normalized =
            value.Trim();

        if (normalized.Length > 100)
        {
            throw new BusinessRuleException(
                "Idempotency key cannot exceed 100 characters.");
        }

        return normalized;
    }

    private static string NormalizeInvestmentType(
        string? value)
    {
        if (string.Equals(
                value?.Trim(),
                InvestmentPlacementTypes.FixedDeposit,
                StringComparison.OrdinalIgnoreCase))
        {
            return InvestmentPlacementTypes.FixedDeposit;
        }

        throw new BusinessRuleException(
            "Investment type must be FixedDeposit.");
    }

    private static string NormalizeStatus(
        string value)
    {
        var status =
            AllowedStatuses.FirstOrDefault(
                allowed =>
                    string.Equals(
                        allowed,
                        value.Trim(),
                        StringComparison.OrdinalIgnoreCase));

        if (status is null)
        {
            throw new BusinessRuleException(
                "Invalid investment placement status.");
        }

        return status;
    }

    private static string NormalizeCurrency(
        string value)
    {
        var currency =
            value.Trim().ToUpperInvariant();

        if (currency.Length != 3)
        {
            throw new BusinessRuleException(
                "Currency must contain exactly three characters.");
        }

        return currency;
    }

    private static string NormalizeRequiredText(
        string? value,
        string fieldName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                $"{fieldName} is required.");
        }

        var normalized =
            value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new BusinessRuleException(
                $"{fieldName} cannot exceed {maximumLength} characters.");
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
            throw new BusinessRuleException(
                $"Value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static DateTime NormalizeUtc(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc =>
                value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc)
        };
    }

    private static InvestmentPlacementResponseDto Map(
        InvestmentPlacement placement)
    {
        return new InvestmentPlacementResponseDto
        {
            Id =
                placement.Id,

            Reference =
                placement.Reference,

            InvestmentType =
                placement.InvestmentType,

            InstitutionName =
                placement.InstitutionName,

            CounterpartyId =
                placement.CounterpartyId,

            CounterpartyCode =
                placement.Counterparty?.Code,

            CounterpartyName =
                placement.Counterparty?.Name,

            SourceAccountId =
                placement.SourceAccountId,

            SourceAccountName =
                placement.SourceAccount?.Name ??
                string.Empty,

            PrincipalAmount =
                placement.PrincipalAmount,

            Currency =
                placement.Currency,

            AnnualInterestRate =
                placement.AnnualInterestRate,

            DayCountBasis =
                placement.DayCountBasis,

            StartDateUtc =
                placement.StartDateUtc,

            MaturityDateUtc =
                placement.MaturityDateUtc,

            TenorDays =
                (placement.MaturityDateUtc -
                 placement.StartDateUtc).Days,

            ExpectedInterestAmount =
                placement.ExpectedInterestAmount,

            ExpectedMaturityAmount =
                placement.ExpectedMaturityAmount,

            Status =
                placement.Status,

            ExternalReference =
                placement.ExternalReference,

            Notes =
                placement.Notes,

            CreatedByUserId =
                placement.CreatedByUserId,

            CreatedAtUtc =
                placement.CreatedAtUtc,

            UpdatedAtUtc =
                placement.UpdatedAtUtc,

            RequiredApprovalCount =
                placement.RequiredApprovalCount,

            ApprovalCount =
                placement.ApprovalCount,

            ActivationRequestedByUserId =
                placement.ActivationRequestedByUserId,

            ActivationRequestedAtUtc =
                placement.ActivationRequestedAtUtc,

            ActivationExpiresAtUtc =
                placement.ActivationExpiresAtUtc,

            ActivationRejectedByUserId =
                placement.ActivationRejectedByUserId,

            ActivationRejectedAtUtc =
                placement.ActivationRejectedAtUtc,

            ActivationRejectionReason =
                placement.ActivationRejectionReason,

            ActivationIdempotencyKey =
                placement.ActivationIdempotencyKey,

            FundingTreasuryTransactionId =
                placement.FundingTreasuryTransactionId,

            FundingTransactionReference =
                placement.FundingTreasuryTransaction?
                    .Reference,

            MaturityForecastItemId =
                placement.MaturityForecastItemId,

            ActivatedByUserId =
                placement.ActivatedByUserId,

            ActivatedAtUtc =
                placement.ActivatedAtUtc,
            
            RedemptionIdempotencyKey =
                placement.RedemptionIdempotencyKey,

            RedemptionAccountId =
                placement.RedemptionAccountId,

            RedemptionAccountName =
                placement.RedemptionAccount?.Name,

            RedemptionTreasuryTransactionId =
                placement.RedemptionTreasuryTransactionId,

            RedemptionTransactionReference =
                placement.RedemptionTreasuryTransaction?
                    .Reference,

            ActualInterestAmount =
                placement.ActualInterestAmount,

            WithholdingTaxAmount =
                placement.WithholdingTaxAmount,

            ActualMaturityAmount =
                placement.ActualMaturityAmount,

            RedemptionExternalReference =
                placement.RedemptionExternalReference,

            RedemptionNotes =
                placement.RedemptionNotes,

            RedeemedByUserId =
                placement.RedeemedByUserId,

            RedeemedAtUtc =
                placement.RedeemedAtUtc,

            CancelledByUserId =
                placement.CancelledByUserId,

            CancelledAtUtc =
                placement.CancelledAtUtc,

            CancellationReason =
                placement.CancellationReason
        };
    }

    private static object Snapshot(
        InvestmentPlacement placement)
    {
        return new
        {
            placement.Id,
            placement.Reference,
            placement.InvestmentType,
            placement.InstitutionName,
            placement.CounterpartyId,
            CounterpartyCode =
                placement.Counterparty?.Code,
            placement.SourceAccountId,
            placement.PrincipalAmount,
            placement.Currency,
            placement.AnnualInterestRate,
            placement.DayCountBasis,
            placement.StartDateUtc,
            placement.MaturityDateUtc,
            placement.ExpectedInterestAmount,
            placement.ExpectedMaturityAmount,
            placement.Status,
            placement.ExternalReference,
            placement.Notes,
            placement.CreatedByUserId,
            placement.CreatedAtUtc,
            placement.UpdatedAtUtc,
            placement.RequiredApprovalCount,
            placement.ApprovalCount,
            placement.ActivationRequestedByUserId,
            placement.ActivationRequestedAtUtc,
            placement.ActivationExpiresAtUtc,
            placement.ActivationRejectedByUserId,
            placement.ActivationRejectedAtUtc,
            placement.ActivationRejectionReason,
            placement.ActivationIdempotencyKey,
            placement.FundingTreasuryTransactionId,
            placement.MaturityForecastItemId,
            placement.ActivatedByUserId,
            placement.ActivatedAtUtc,
            placement.RedemptionIdempotencyKey,
            placement.RedemptionAccountId,
            placement.RedemptionTreasuryTransactionId,
            placement.ActualInterestAmount,
            placement.WithholdingTaxAmount,
            placement.ActualMaturityAmount,
            placement.RedemptionExternalReference,
            placement.RedemptionNotes,
            placement.RedeemedByUserId,
            placement.RedeemedAtUtc,
            placement.CancelledByUserId,
            placement.CancelledAtUtc,
            placement.CancellationReason
        };
    }
}