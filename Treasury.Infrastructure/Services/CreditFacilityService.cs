using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.CreditFacilities;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class CreditFacilityService
    : ICreditFacilityService
{
    private static readonly string[] AllowedTypes =
    {
        CreditFacilityTypes.Overdraft,
        CreditFacilityTypes.RevolvingCredit,
        CreditFacilityTypes.TermLoan
    };

    private static readonly string[] AllowedFrequencies =
    {
        FacilityInterestPaymentFrequencies.Monthly,
        FacilityInterestPaymentFrequencies.Quarterly,
        FacilityInterestPaymentFrequencies.SemiAnnual,
        FacilityInterestPaymentFrequencies.Annual,
        FacilityInterestPaymentFrequencies.AtMaturity
    };

    private static readonly string[] AllowedStatuses =
    {
        CreditFacilityStatuses.Draft,
        CreditFacilityStatuses.PendingActivation,
        CreditFacilityStatuses.Active,
        CreditFacilityStatuses.Suspended,
        CreditFacilityStatuses.Matured,
        CreditFacilityStatuses.Closed,
        CreditFacilityStatuses.ActivationRejected,
        CreditFacilityStatuses.ActivationExpired,
        CreditFacilityStatuses.Cancelled
    };

    private readonly ICreditFacilityRepository
        _facilityRepository;

    private readonly ICounterpartyRepository
        _counterpartyRepository;

    private readonly IAccountRepository
        _accountRepository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IApprovalPolicyService
        _approvalPolicyService;

    private readonly IApprovalDecisionRepository
        _approvalDecisionRepository;

    private readonly IAuditLogService
        _auditLogService;

    public CreditFacilityService(
        ICreditFacilityRepository facilityRepository,
        ICounterpartyRepository counterpartyRepository,
        IAccountRepository accountRepository,
        ICurrentUserService currentUserService,
        IApprovalPolicyService approvalPolicyService,
        IApprovalDecisionRepository approvalDecisionRepository,
        IAuditLogService auditLogService)
    {
        _facilityRepository =
            facilityRepository;

        _counterpartyRepository =
            counterpartyRepository;

        _accountRepository =
            accountRepository;

        _currentUserService =
            currentUserService;

        _approvalPolicyService =
            approvalPolicyService;

        _approvalDecisionRepository =
            approvalDecisionRepository;

        _auditLogService =
            auditLogService;
    }

    public async Task<CreditFacilityResponseDto> Create(
        CreateCreditFacilityDto dto)
    {
        var facilityName =
            NormalizeRequiredText(
                dto.FacilityName,
                "Facility name",
                200);

        var facilityType =
            NormalizeAllowedValue(
                dto.FacilityType,
                AllowedTypes,
                "facility type");

        var paymentFrequency =
            NormalizeAllowedValue(
                dto.InterestPaymentFrequency,
                AllowedFrequencies,
                "interest payment frequency");

        ValidateFinancialTerms(
            dto.ApprovedLimitAmount,
            dto.AnnualInterestRate,
            dto.CommitmentFeeRatePercentage,
            dto.ArrangementFeeAmount,
            dto.DayCountBasis);

        var startDateUtc =
            NormalizeUtc(dto.StartDateUtc).Date;

        var maturityDateUtc =
            NormalizeUtc(dto.MaturityDateUtc).Date;

        ValidateDates(
            startDateUtc,
            maturityDateUtc);

        var lender =
            await GetActiveLender(
                dto.LenderCounterpartyId);

        var account =
            await GetActiveSettlementAccount(
                dto.SettlementAccountId);

        var now = DateTime.UtcNow;

        var facility =
            new CreditFacility
            {
                Id = Guid.NewGuid(),

                Reference =
                    await GenerateReference(),

                FacilityName =
                    facilityName,

                FacilityType =
                    facilityType,

                LenderCounterpartyId =
                    lender.Id,

                LenderCounterparty =
                    lender,

                LenderName =
                    lender.Name,

                SettlementAccountId =
                    account.Id,

                SettlementAccount =
                    account,

                /*
                 * Currency is controlled by the selected
                 * settlement account.
                 */
                Currency =
                    account.Currency.Trim()
                        .ToUpperInvariant(),

                ApprovedLimitAmount =
                    dto.ApprovedLimitAmount,

                OutstandingPrincipalAmount = 0m,

                AccruedInterestAmount = 0m,

                AnnualInterestRate =
                    dto.AnnualInterestRate,

                CommitmentFeeRatePercentage =
                    dto.CommitmentFeeRatePercentage,

                ArrangementFeeAmount =
                    dto.ArrangementFeeAmount,

                DayCountBasis =
                    dto.DayCountBasis,

                InterestPaymentFrequency =
                    paymentFrequency,

                StartDateUtc =
                    startDateUtc,

                MaturityDateUtc =
                    maturityDateUtc,

                Status =
                    CreditFacilityStatuses.Draft,

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

                CreatedAtUtc = now,

                UpdatedAtUtc = now,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        await _facilityRepository.Add(facility);
        await _facilityRepository.SaveChanges();

        await RecordAudit(
            facility,
            AuditActionTypes.Created,
            $"Credit facility {facility.Reference} was created.",
            beforeValues: null,
            afterValues: Snapshot(facility));

        return Map(facility);
    }

    public async Task<CreditFacilityResponseDto> GetById(
        Guid id)
    {
        var facility =
            await GetFacility(id);

        return Map(facility);
    }

    public async Task<PagedCreditFacilityResponseDto>
        Search(CreditFacilityQueryDto query)
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
                NormalizeAllowedValue(
                    query.Status,
                    AllowedStatuses,
                    "facility status");
        }

        if (!string.IsNullOrWhiteSpace(
                query.FacilityType))
        {
            query.FacilityType =
                NormalizeAllowedValue(
                    query.FacilityType,
                    AllowedTypes,
                    "facility type");
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            query.Currency =
                query.Currency.Trim()
                    .ToUpperInvariant();

            if (query.Currency.Length != 3)
            {
                throw new BusinessRuleException(
                    "Currency must contain three characters.");
            }
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
            query.MaturityFromUtc >
                query.MaturityToUtc)
        {
            throw new BusinessRuleException(
                "Maturity-from date cannot be later " +
                "than maturity-to date.");
        }

        var result =
            await _facilityRepository.Search(query);

        return new PagedCreditFacilityResponseDto
        {
            Items =
                result.Items.Select(Map).ToList(),

            Page =
                query.Page,

            PageSize =
                query.PageSize,

            TotalCount =
                result.TotalCount,

            TotalPages =
                (int)Math.Ceiling(
                    result.TotalCount /
                    (double)query.PageSize)
        };
    }

    public async Task<CreditFacilityResponseDto> Update(
        Guid id,
        UpdateCreditFacilityDto dto)
    {
        var facility =
            await GetFacility(id);

        if (facility.Status !=
            CreditFacilityStatuses.Draft)
        {
            throw new ConflictException(
                "Only a draft credit facility can be updated.");
        }

        var facilityName =
            NormalizeRequiredText(
                dto.FacilityName,
                "Facility name",
                200);

        var facilityType =
            NormalizeAllowedValue(
                dto.FacilityType,
                AllowedTypes,
                "facility type");

        var paymentFrequency =
            NormalizeAllowedValue(
                dto.InterestPaymentFrequency,
                AllowedFrequencies,
                "interest payment frequency");

        ValidateFinancialTerms(
            dto.ApprovedLimitAmount,
            dto.AnnualInterestRate,
            dto.CommitmentFeeRatePercentage,
            dto.ArrangementFeeAmount,
            dto.DayCountBasis);

        var startDateUtc =
            NormalizeUtc(dto.StartDateUtc).Date;

        var maturityDateUtc =
            NormalizeUtc(dto.MaturityDateUtc).Date;

        ValidateDates(
            startDateUtc,
            maturityDateUtc);

        var lender =
            await GetActiveLender(
                dto.LenderCounterpartyId);

        var account =
            await GetActiveSettlementAccount(
                dto.SettlementAccountId);

        var beforeValues =
            Snapshot(facility);

        facility.FacilityName =
            facilityName;

        facility.FacilityType =
            facilityType;

        facility.LenderCounterpartyId =
            lender.Id;

        facility.LenderCounterparty =
            lender;

        facility.LenderName =
            lender.Name;

        facility.SettlementAccountId =
            account.Id;

        facility.SettlementAccount =
            account;

        facility.Currency =
            account.Currency.Trim()
                .ToUpperInvariant();

        facility.ApprovedLimitAmount =
            dto.ApprovedLimitAmount;

        facility.AnnualInterestRate =
            dto.AnnualInterestRate;

        facility.CommitmentFeeRatePercentage =
            dto.CommitmentFeeRatePercentage;

        facility.ArrangementFeeAmount =
            dto.ArrangementFeeAmount;

        facility.DayCountBasis =
            dto.DayCountBasis;

        facility.InterestPaymentFrequency =
            paymentFrequency;

        facility.StartDateUtc =
            startDateUtc;

        facility.MaturityDateUtc =
            maturityDateUtc;

        facility.ExternalReference =
            NormalizeOptionalText(
                dto.ExternalReference,
                100);

        facility.Notes =
            NormalizeOptionalText(
                dto.Notes,
                1000);

        facility.UpdatedByUserId =
            _currentUserService.UserId;

        facility.UpdatedAtUtc =
            DateTime.UtcNow;

        facility.ConcurrencyToken =
            Guid.NewGuid();

        _facilityRepository.Update(facility);

        try
        {
            await _facilityRepository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The credit facility was changed by " +
                "another operation.");
        }

        await RecordAudit(
            facility,
            AuditActionTypes.Updated,
            $"Credit facility {facility.Reference} was updated.",
            beforeValues,
            Snapshot(facility));

        return Map(facility);
    }

    public async Task<CreditFacilityResponseDto> Activate(
        Guid id,
        string idempotencyKey)
    {
        var normalizedKey =
            NormalizeIdempotencyKey(idempotencyKey);

        var facility =
            await GetFacility(id);

        /*
         * A repeated request with the same key returns
         * the original result.
         */
        if ((facility.Status ==
                CreditFacilityStatuses.PendingActivation ||
             facility.Status ==
                CreditFacilityStatuses.Active) &&
            string.Equals(
                facility.ActivationIdempotencyKey,
                normalizedKey,
                StringComparison.Ordinal))
        {
            return Map(facility);
        }

        if (facility.Status !=
            CreditFacilityStatuses.Draft)
        {
            throw new ConflictException(
                "Only a draft credit facility can be " +
                "submitted for activation.");
        }

        var existingKey =
            await _facilityRepository
                .GetByActivationIdempotencyKey(
                    normalizedKey);

        if (existingKey is not null &&
            existingKey.Id != facility.Id)
        {
            throw new ConflictException(
                "The idempotency key has already been used.");
        }

        ValidateFacilityForActivation(facility);

        var requirements =
            await _approvalPolicyService
                .GetRequirements(
                    ApprovalOperationTypes
                        .CreditFacilityActivation,
                    facility.Currency);

        var beforeValues =
            Snapshot(facility);

        var now = DateTime.UtcNow;

        facility.ActivationIdempotencyKey =
            normalizedKey;

        facility.ActivationRequestedByUserId =
            _currentUserService.UserId;

        facility.ActivationRequestedAtUtc =
            now;

        facility.UpdatedByUserId =
            _currentUserService.UserId;

        facility.UpdatedAtUtc =
            now;

        facility.ApprovalCount = 0;

        if (facility.ApprovedLimitAmount >
            requirements.ThresholdAmount)
        {
            facility.Status =
                CreditFacilityStatuses
                    .PendingActivation;

            facility.RequiredApprovalCount =
                requirements.RequiredApprovalCount;

            facility.ActivationExpiresAtUtc =
                now.AddHours(
                    requirements
                        .PendingRequestExpiryHours);
        }
        else
        {
            facility.Status =
                CreditFacilityStatuses.Active;

            facility.RequiredApprovalCount = 0;

            facility.ActivationExpiresAtUtc = null;

            facility.ActivatedByUserId =
                _currentUserService.UserId;

            facility.ActivatedAtUtc = now;
        }

        facility.ConcurrencyToken =
            Guid.NewGuid();

        _facilityRepository.Update(facility);

        try
        {
            await _facilityRepository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The credit facility changed while " +
                "activation was being submitted.");
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The activation could not be saved. " +
                "The idempotency key may already be in use.");
        }

        var immediatelyActivated =
            facility.Status ==
                CreditFacilityStatuses.Active;

        await RecordAudit(
            facility,
            immediatelyActivated
                ? AuditActionTypes.Activated
                : AuditActionTypes.Updated,
            immediatelyActivated
                ? $"Credit facility {facility.Reference} was activated."
                : $"Credit facility {facility.Reference} was submitted for activation approval.",
            beforeValues,
            Snapshot(facility));

        return Map(facility);
    }

    public async Task<CreditFacilityResponseDto>
        ApproveActivation(Guid id)
    {
        var facility =
            await GetPendingActivation(id);

        EnsureDifferentReviewer(
            facility.ActivationRequestedByUserId);

        var currentUserId =
            _currentUserService.UserId;

        var alreadyReviewed =
            await _approvalDecisionRepository
                .HasCreditFacilityDecision(
                    facility.Id,
                    currentUserId);

        if (alreadyReviewed)
        {
            throw new ConflictException(
                "You have already reviewed this " +
                "credit facility activation.");
        }

        var beforeValues =
            Snapshot(facility);

        await _approvalDecisionRepository.Add(
            new ApprovalDecision
            {
                Id = Guid.NewGuid(),

                CreditFacilityId =
                    facility.Id,

                ApproverUserId =
                    currentUserId,

                Decision =
                    ApprovalDecisionTypes.Approved,

                CreatedAtUtc =
                    DateTime.UtcNow
            });

        facility.ApprovalCount += 1;

        facility.UpdatedByUserId =
            currentUserId;

        facility.UpdatedAtUtc =
            DateTime.UtcNow;

        var isFinalApproval =
            facility.ApprovalCount >=
                facility.RequiredApprovalCount;

        if (isFinalApproval)
        {
            ValidateFacilityForActivation(facility);

            facility.Status =
                CreditFacilityStatuses.Active;

            facility.ActivatedByUserId =
                currentUserId;

            facility.ActivatedAtUtc =
                DateTime.UtcNow;
        }

        facility.ConcurrencyToken =
            Guid.NewGuid();

        _facilityRepository.Update(facility);

        try
        {
            /*
             * The facility update and approval decision use
             * the same DbContext and are saved together.
             */
            await _facilityRepository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The facility changed while approval " +
                "was being processed.");
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The approval could not be saved. You may " +
                "have already reviewed this request.");
        }

        await RecordAudit(
            facility,
            isFinalApproval
                ? AuditActionTypes.Activated
                : AuditActionTypes.Approved,
            isFinalApproval
                ? $"Credit facility {facility.Reference} received final approval and was activated."
                : $"Credit facility {facility.Reference} received partial approval.",
            beforeValues,
            Snapshot(facility));

        return Map(facility);
    }

    public async Task<CreditFacilityResponseDto>
        RejectActivation(
            Guid id,
            string reason)
    {
        var rejectionReason =
            NormalizeRequiredText(
                reason,
                "Rejection reason",
                500);

        var facility =
            await GetPendingActivation(id);

        EnsureDifferentReviewer(
            facility.ActivationRequestedByUserId);

        var currentUserId =
            _currentUserService.UserId;

        var alreadyReviewed =
            await _approvalDecisionRepository
                .HasCreditFacilityDecision(
                    facility.Id,
                    currentUserId);

        if (alreadyReviewed)
        {
            throw new ConflictException(
                "You have already reviewed this " +
                "credit facility activation.");
        }

        var beforeValues =
            Snapshot(facility);

        await _approvalDecisionRepository.Add(
            new ApprovalDecision
            {
                Id = Guid.NewGuid(),

                CreditFacilityId =
                    facility.Id,

                ApproverUserId =
                    currentUserId,

                Decision =
                    ApprovalDecisionTypes.Rejected,

                Comment =
                    rejectionReason,

                CreatedAtUtc =
                    DateTime.UtcNow
            });

        facility.Status =
            CreditFacilityStatuses
                .ActivationRejected;

        facility.ActivationRejectedByUserId =
            currentUserId;

        facility.ActivationRejectedAtUtc =
            DateTime.UtcNow;

        facility.ActivationRejectionReason =
            rejectionReason;

        facility.UpdatedByUserId =
            currentUserId;

        facility.UpdatedAtUtc =
            DateTime.UtcNow;

        facility.ConcurrencyToken =
            Guid.NewGuid();

        _facilityRepository.Update(facility);

        try
        {
            await _facilityRepository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The facility changed while rejection " +
                "was being processed.");
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The rejection could not be saved. You may " +
                "have already reviewed this request.");
        }

        await RecordAudit(
            facility,
            AuditActionTypes.Rejected,
            $"Credit facility {facility.Reference} activation was rejected.",
            beforeValues,
            Snapshot(facility));

        return Map(facility);
    }

    public async Task<CreditFacilityResponseDto> Cancel(
        Guid id,
        string reason)
    {
        var cancellationReason =
            NormalizeRequiredText(
                reason,
                "Cancellation reason",
                500);

        var facility =
            await GetFacility(id);

        if (facility.Status !=
                CreditFacilityStatuses.Draft &&
            facility.Status !=
                CreditFacilityStatuses.ActivationRejected &&
            facility.Status !=
                CreditFacilityStatuses.ActivationExpired)
        {
            throw new ConflictException(
                "Only a draft, rejected or expired credit " +
                "facility can be cancelled.");
        }

        var beforeValues =
            Snapshot(facility);

        facility.Status =
            CreditFacilityStatuses.Cancelled;

        facility.CancelledByUserId =
            _currentUserService.UserId;

        facility.CancelledAtUtc =
            DateTime.UtcNow;

        facility.CancellationReason =
            cancellationReason;

        facility.UpdatedByUserId =
            _currentUserService.UserId;

        facility.UpdatedAtUtc =
            DateTime.UtcNow;

        facility.ConcurrencyToken =
            Guid.NewGuid();

        _facilityRepository.Update(facility);

        try
        {
            await _facilityRepository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The credit facility changed while " +
                "cancellation was being processed.");
        }

        await RecordAudit(
            facility,
            AuditActionTypes.Cancelled,
            $"Credit facility {facility.Reference} was cancelled.",
            beforeValues,
            Snapshot(facility));

        return Map(facility);
    }

    private async Task<CreditFacility> GetFacility(
        Guid id)
    {
        var facility =
            await _facilityRepository.GetById(id);

        if (facility is null)
        {
            throw new ResourceNotFoundException(
                "Credit facility was not found.");
        }

        return facility;
    }

    private async Task<CreditFacility>
        GetPendingActivation(Guid id)
    {
        var facility =
            await GetFacility(id);

        if (facility.Status !=
            CreditFacilityStatuses.PendingActivation)
        {
            throw new ConflictException(
                "The credit facility is not awaiting " +
                "activation approval.");
        }

        if (facility.ActivationExpiresAtUtc.HasValue &&
            facility.ActivationExpiresAtUtc.Value <=
                DateTime.UtcNow)
        {
            var beforeValues =
                Snapshot(facility);

            facility.Status =
                CreditFacilityStatuses.ActivationExpired;

            facility.UpdatedAtUtc =
                DateTime.UtcNow;

            facility.UpdatedByUserId =
                _currentUserService.UserId;

            facility.ConcurrencyToken =
                Guid.NewGuid();

            _facilityRepository.Update(facility);

            await _facilityRepository.SaveChanges();

            await RecordAudit(
                facility,
                AuditActionTypes.Expired,
                $"Credit facility {facility.Reference} activation request expired.",
                beforeValues,
                Snapshot(facility));

            throw new ConflictException(
                "The credit facility activation request " +
                "has expired.");
        }

        return facility;
    }

    private async Task<Counterparty> GetActiveLender(
        Guid counterpartyId)
    {
        if (counterpartyId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Lender counterparty ID is required.");
        }

        var lender =
            await _counterpartyRepository
                .GetById(counterpartyId);

        if (lender is null)
        {
            throw new ResourceNotFoundException(
                "Lender counterparty was not found.");
        }

        if (!lender.IsActive)
        {
            throw new ConflictException(
                "The lender counterparty is inactive.");
        }

        return lender;
    }

    private async Task<Account>
        GetActiveSettlementAccount(
            Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Settlement account ID is required.");
        }

        var account =
            await _accountRepository.GetById(accountId);

        if (account is null)
        {
            throw new ResourceNotFoundException(
                "Settlement account was not found.");
        }

        if (!account.IsActive)
        {
            throw new ConflictException(
                "The settlement account is inactive.");
        }

        return account;
    }

    private static void ValidateFacilityForActivation(
        CreditFacility facility)
    {
        if (!facility.LenderCounterparty.IsActive)
        {
            throw new ConflictException(
                "The lender counterparty is inactive.");
        }

        if (!facility.SettlementAccount.IsActive)
        {
            throw new ConflictException(
                "The settlement account is inactive.");
        }

        if (!string.Equals(
                facility.Currency,
                facility.SettlementAccount.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "The facility currency does not match " +
                "the settlement account currency.");
        }

        if (facility.MaturityDateUtc.Date <=
            DateTime.UtcNow.Date)
        {
            throw new BusinessRuleException(
                "The facility has reached its maturity date.");
        }

        if (facility.OutstandingPrincipalAmount >
            facility.ApprovedLimitAmount)
        {
            throw new BusinessRuleException(
                "Outstanding principal cannot exceed " +
                "the approved facility limit.");
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
                "You cannot approve or reject your own " +
                "credit facility activation request.");
        }
    }

    private async Task<string> GenerateReference()
    {
        for (var attempt = 0;
             attempt < 10;
             attempt++)
        {
            var reference =
                $"FAC-{DateTime.UtcNow:yyyyMMdd}-" +
                Guid.NewGuid()
                    .ToString("N")[..8]
                    .ToUpperInvariant();

            if (!await _facilityRepository
                    .ReferenceExists(reference))
            {
                return reference;
            }
        }

        throw new ConflictException(
            "Unable to generate a unique facility reference.");
    }

    private static void ValidateFinancialTerms(
        decimal approvedLimitAmount,
        decimal annualInterestRate,
        decimal commitmentFeeRate,
        decimal arrangementFeeAmount,
        int dayCountBasis)
    {
        if (approvedLimitAmount <= 0)
        {
            throw new BusinessRuleException(
                "Approved limit amount must be greater than zero.");
        }

        if (annualInterestRate < 0 ||
            annualInterestRate > 100)
        {
            throw new BusinessRuleException(
                "Annual interest rate must be between 0 and 100.");
        }

        if (commitmentFeeRate < 0 ||
            commitmentFeeRate > 100)
        {
            throw new BusinessRuleException(
                "Commitment fee rate must be between 0 and 100.");
        }

        if (arrangementFeeAmount < 0)
        {
            throw new BusinessRuleException(
                "Arrangement fee amount cannot be negative.");
        }

        if (dayCountBasis != 360 &&
            dayCountBasis != 365)
        {
            throw new BusinessRuleException(
                "Day-count basis must be either 360 or 365.");
        }
    }

    private static void ValidateDates(
        DateTime startDateUtc,
        DateTime maturityDateUtc)
    {
        if (maturityDateUtc <= startDateUtc)
        {
            throw new BusinessRuleException(
                "Maturity date must be later than start date.");
        }

        if (maturityDateUtc <=
            DateTime.UtcNow.Date)
        {
            throw new BusinessRuleException(
                "Maturity date must be in the future.");
        }

        if ((maturityDateUtc -
             startDateUtc).TotalDays > 10950)
        {
            throw new BusinessRuleException(
                "Facility tenor cannot exceed 30 years.");
        }
    }

    private static string NormalizeAllowedValue(
        string value,
        IEnumerable<string> allowedValues,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                $"{fieldName} is required.");
        }

        var normalized =
            allowedValues.FirstOrDefault(item =>
                string.Equals(
                    item,
                    value.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        if (normalized is null)
        {
            throw new BusinessRuleException(
                $"Invalid {fieldName}.");
        }

        return normalized;
    }

    private static string NormalizeRequiredText(
        string value,
        string fieldName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                $"{fieldName} is required.");
        }

        var normalized = value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new BusinessRuleException(
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

        var normalized = value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new BusinessRuleException(
                $"Value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static string NormalizeIdempotencyKey(
        string idempotencyKey)
    {
        return NormalizeRequiredText(
            idempotencyKey,
            "Idempotency key",
            100);
    }

    private static DateTime NormalizeUtc(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ => DateTime.SpecifyKind(
                value,
                DateTimeKind.Utc)
        };
    }

    private async Task RecordAudit(
        CreditFacility facility,
        string action,
        string summary,
        object? beforeValues,
        object? afterValues)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    action,

                EntityType =
                    AuditEntityTypes.CreditFacility,

                EntityId =
                    facility.Id,

                EntityReference =
                    facility.Reference,

                Summary =
                    summary,

                BeforeValues =
                    beforeValues,

                AfterValues =
                    afterValues,

                Metadata =
                    new
                    {
                        Module =
                            "Credit Facilities",

                        facility.LenderCounterpartyId,

                        facility.Currency,

                        facility.ApprovedLimitAmount
                    }
            });
    }

    private static object Snapshot(
        CreditFacility facility)
    {
        return new
        {
            facility.Id,
            facility.Reference,
            facility.FacilityName,
            facility.FacilityType,
            facility.LenderCounterpartyId,
            facility.LenderName,
            facility.SettlementAccountId,
            facility.Currency,
            facility.ApprovedLimitAmount,
            facility.OutstandingPrincipalAmount,
            facility.AccruedInterestAmount,
            facility.AnnualInterestRate,
            facility.CommitmentFeeRatePercentage,
            facility.ArrangementFeeAmount,
            facility.DayCountBasis,
            facility.InterestPaymentFrequency,
            facility.StartDateUtc,
            facility.MaturityDateUtc,
            facility.Status,
            facility.RequiredApprovalCount,
            facility.ApprovalCount,
            facility.ActivationRequestedByUserId,
            facility.ActivationRequestedAtUtc,
            facility.ActivationExpiresAtUtc,
            facility.ActivationRejectedByUserId,
            facility.ActivationRejectedAtUtc,
            facility.ActivationRejectionReason,
            facility.ActivatedByUserId,
            facility.ActivatedAtUtc,
            facility.CancelledByUserId,
            facility.CancelledAtUtc,
            facility.CancellationReason,
            facility.UpdatedAtUtc
        };
    }

    private static CreditFacilityResponseDto Map(
        CreditFacility facility)
    {
        return new CreditFacilityResponseDto
        {
            Id =
                facility.Id,

            Reference =
                facility.Reference,

            FacilityName =
                facility.FacilityName,

            FacilityType =
                facility.FacilityType,

            LenderCounterpartyId =
                facility.LenderCounterpartyId,

            LenderCode =
                facility.LenderCounterparty?.Code
                ?? string.Empty,

            LenderName =
                facility.LenderName,

            SettlementAccountId =
                facility.SettlementAccountId,

            SettlementAccountName =
                facility.SettlementAccount?.Name
                ?? string.Empty,

            SettlementAccountNumber =
                facility.SettlementAccount?.AccountNumber
                ?? string.Empty,

            Currency =
                facility.Currency,

            ApprovedLimitAmount =
                facility.ApprovedLimitAmount,

            OutstandingPrincipalAmount =
                facility.OutstandingPrincipalAmount,

            AccruedInterestAmount =
                facility.AccruedInterestAmount,

            AvailableAmount =
                facility.AvailableAmount,

            TotalOutstandingAmount =
                facility.TotalOutstandingAmount,

            AnnualInterestRate =
                facility.AnnualInterestRate,

            CommitmentFeeRatePercentage =
                facility.CommitmentFeeRatePercentage,

            ArrangementFeeAmount =
                facility.ArrangementFeeAmount,

            DayCountBasis =
                facility.DayCountBasis,

            InterestPaymentFrequency =
                facility.InterestPaymentFrequency,

            StartDateUtc =
                facility.StartDateUtc,

            MaturityDateUtc =
                facility.MaturityDateUtc,

            TenorDays =
                (facility.MaturityDateUtc -
                 facility.StartDateUtc).Days,

            Status =
                facility.Status,

            ExternalReference =
                facility.ExternalReference,

            Notes =
                facility.Notes,

            CreatedByUserId =
                facility.CreatedByUserId,

            UpdatedByUserId =
                facility.UpdatedByUserId,

            CreatedAtUtc =
                facility.CreatedAtUtc,

            UpdatedAtUtc =
                facility.UpdatedAtUtc,

            RequiredApprovalCount =
                facility.RequiredApprovalCount,

            ApprovalCount =
                facility.ApprovalCount,

            ActivationRequestedByUserId =
                facility.ActivationRequestedByUserId,

            ActivationRequestedAtUtc =
                facility.ActivationRequestedAtUtc,

            ActivationExpiresAtUtc =
                facility.ActivationExpiresAtUtc,

            ActivationIdempotencyKey =
                facility.ActivationIdempotencyKey,

            ActivationRejectedByUserId =
                facility.ActivationRejectedByUserId,

            ActivationRejectedAtUtc =
                facility.ActivationRejectedAtUtc,

            ActivationRejectionReason =
                facility.ActivationRejectionReason,

            ActivatedByUserId =
                facility.ActivatedByUserId,

            ActivatedAtUtc =
                facility.ActivatedAtUtc,

            SuspendedByUserId =
                facility.SuspendedByUserId,

            SuspendedAtUtc =
                facility.SuspendedAtUtc,

            SuspensionReason =
                facility.SuspensionReason,

            MaturedAtUtc =
                facility.MaturedAtUtc,

            ClosedByUserId =
                facility.ClosedByUserId,

            ClosedAtUtc =
                facility.ClosedAtUtc,

            ClosureReason =
                facility.ClosureReason,

            CancelledByUserId =
                facility.CancelledByUserId,

            CancelledAtUtc =
                facility.CancelledAtUtc,

            CancellationReason =
                facility.CancellationReason
        };
    }
}