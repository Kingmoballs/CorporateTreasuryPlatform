using Microsoft.EntityFrameworkCore;
using Treasury.Shared.Common;
using Treasury.Application.Common.Exceptions;
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

    public InvestmentPlacementService(
        IInvestmentPlacementRepository placementRepository,
        IAccountRepository accountRepository,
        ITreasuryTransactionRepository transactionRepository,
        ILedgerRepository ledgerRepository,
        ICashFlowForecastRepository forecastRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IApprovalPolicyService approvalPolicyService,
        IApprovalDecisionRepository approvalDecisionRepository)
    {
        _placementRepository =
            placementRepository;

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

        var institutionName =
            NormalizeRequiredText(
                dto.InstitutionName,
                "Institution name",
                200);

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
                    institutionName,

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
            placement.CancelledByUserId,
            placement.CancelledAtUtc,
            placement.CancellationReason
        };
    }
}