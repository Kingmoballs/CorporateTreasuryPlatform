using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.CreditFacilityLifecycle;
using Treasury.Application.DTOs.TreasuryAlerts;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class CreditFacilityLifecycleService
    : ICreditFacilityLifecycleService
{
    private readonly ICreditFacilityRepository
        _facilityRepository;

    private readonly ITreasuryAlertRepository
        _alertRepository;

    private readonly ITreasuryAlertService
        _alertService;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuditLogService
        _auditLogService;

    public CreditFacilityLifecycleService(
        ICreditFacilityRepository facilityRepository,
        ITreasuryAlertRepository alertRepository,
        ITreasuryAlertService alertService,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _facilityRepository =
            facilityRepository;

        _alertRepository =
            alertRepository;

        _alertService =
            alertService;

        _currentUserService =
            currentUserService;

        _auditLogService =
            auditLogService;
    }

    public async Task<CreditFacilityLifecycleResponseDto>
        Suspend(
            Guid creditFacilityId,
            string reason)
    {
        var suspensionReason =
            NormalizeRequiredText(
                reason,
                "Suspension reason",
                500);

        var facility =
            await GetFacility(creditFacilityId);

        if (facility.Status !=
            CreditFacilityStatuses.Active)
        {
            throw new ConflictException(
                "Only an active credit facility can " +
                "be suspended.");
        }

        var beforeValues =
            Snapshot(facility);

        var now =
            DateTime.UtcNow;

        facility.Status =
            CreditFacilityStatuses.Suspended;

        facility.SuspendedByUserId =
            _currentUserService.UserId;

        facility.SuspendedAtUtc =
            now;

        facility.SuspensionReason =
            suspensionReason;

        facility.UpdatedByUserId =
            _currentUserService.UserId;

        facility.UpdatedAtUtc =
            now;

        facility.ConcurrencyToken =
            Guid.NewGuid();

        await SaveFacility(
            facility,
            "The facility changed while suspension " +
            "was being processed.");

        await RecordAudit(
            facility,
            AuditActionTypes.Suspended,
            $"Credit facility {facility.Reference} was suspended.",
            beforeValues,
            new
            {
                Facility = Snapshot(facility),
                Reason = suspensionReason
            });

        return Map(facility);
    }

    public async Task<CreditFacilityLifecycleResponseDto>
        Reactivate(
            Guid creditFacilityId,
            string reason)
    {
        var reactivationReason =
            NormalizeRequiredText(
                reason,
                "Reactivation reason",
                500);

        var facility =
            await GetFacility(creditFacilityId);

        if (facility.Status !=
            CreditFacilityStatuses.Suspended)
        {
            throw new ConflictException(
                "Only a suspended credit facility can " +
                "be reactivated.");
        }

        if (facility.MaturityDateUtc.Date <=
            DateTime.UtcNow.Date)
        {
            throw new ConflictException(
                "A matured credit facility cannot " +
                "be reactivated.");
        }

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

        var beforeValues =
            Snapshot(facility);

        facility.Status =
            CreditFacilityStatuses.Active;

        facility.UpdatedByUserId =
            _currentUserService.UserId;

        facility.UpdatedAtUtc =
            DateTime.UtcNow;

        facility.ConcurrencyToken =
            Guid.NewGuid();

        /*
         * Suspension fields are retained as historical
         * information about the latest suspension.
         */
        await SaveFacility(
            facility,
            "The facility changed while reactivation " +
            "was being processed.");

        await RecordAudit(
            facility,
            AuditActionTypes.Reactivated,
            $"Credit facility {facility.Reference} was reactivated.",
            beforeValues,
            new
            {
                Facility = Snapshot(facility),
                Reason = reactivationReason
            });

        return Map(facility);
    }

    public async Task<CreditFacilityLifecycleResponseDto>
        Close(
            Guid creditFacilityId,
            string reason)
    {
        var closureReason =
            NormalizeRequiredText(
                reason,
                "Closure reason",
                500);

        var facility =
            await GetFacility(creditFacilityId);

        var closableStatus =
            facility.Status ==
                CreditFacilityStatuses.Active ||
            facility.Status ==
                CreditFacilityStatuses.Suspended ||
            facility.Status ==
                CreditFacilityStatuses.Matured;

        if (!closableStatus)
        {
            throw new ConflictException(
                "The facility is not in a state that " +
                "allows closure.");
        }

        var principal =
            Math.Round(
                facility.OutstandingPrincipalAmount,
                2);

        var interest =
            Math.Round(
                facility.AccruedInterestAmount,
                2);

        if (principal != 0m ||
            interest != 0m)
        {
            throw new BusinessRuleException(
                "The facility cannot be closed while " +
                "principal or accrued interest remains " +
                "outstanding.");
        }

        var beforeValues =
            Snapshot(facility);

        var now =
            DateTime.UtcNow;

        facility.Status =
            CreditFacilityStatuses.Closed;

        facility.ClosedByUserId =
            _currentUserService.UserId;

        facility.ClosedAtUtc =
            now;

        facility.ClosureReason =
            closureReason;

        facility.UpdatedByUserId =
            _currentUserService.UserId;

        facility.UpdatedAtUtc =
            now;

        facility.ConcurrencyToken =
            Guid.NewGuid();

        await SaveFacility(
            facility,
            "The facility changed while closure " +
            "was being processed.");

        /*
         * Resolve an existing overdue alert when the
         * facility is closed with no remaining debt.
         */
        var overdueAlert =
            await _alertRepository.GetOpenAlert(
                TreasuryAlertTypes
                    .CreditFacilityDebtOverdue,
                AuditEntityTypes.CreditFacility,
                facility.Id,
                facility.Reference);

        if (overdueAlert is not null)
        {
            await _alertService.Resolve(
                overdueAlert.Id,
                "Facility closed with no outstanding debt.");
        }

        await RecordAudit(
            facility,
            AuditActionTypes.Closed,
            $"Credit facility {facility.Reference} was closed.",
            beforeValues,
            new
            {
                Facility = Snapshot(facility),
                Reason = closureReason
            });

        return Map(facility);
    }

    public async Task<
        CreditFacilityMaturityProcessingResultDto>
        ProcessMaturities(
            ProcessCreditFacilityMaturitiesDto dto)
    {
        if (dto.MaxRows < 1 ||
            dto.MaxRows > 1000)
        {
            throw new BusinessRuleException(
                "Max rows must be between 1 and 1000.");
        }

        var asOfDateUtc =
            NormalizeUtc(
                dto.AsOfDateUtc ??
                DateTime.UtcNow).Date;

        if (asOfDateUtc >
            DateTime.UtcNow.Date)
        {
            throw new BusinessRuleException(
                "Maturities cannot be processed for " +
                "a future date.");
        }

        var facilities =
            await _facilityRepository
                .GetDueForMaturity(
                    asOfDateUtc,
                    dto.MaxRows);

        var result =
            new CreditFacilityMaturityProcessingResultDto
            {
                AsOfDateUtc =
                    asOfDateUtc,

                FacilitiesSelected =
                    facilities.Count
            };

        if (facilities.Count == 0)
        {
            return result;
        }

        var beforeValues =
            facilities.ToDictionary(
                facility => facility.Id,
                Snapshot);

        var now =
            DateTime.UtcNow;

        foreach (var facility in facilities)
        {
            facility.Status =
                CreditFacilityStatuses.Matured;

            facility.MaturedAtUtc =
                now;

            facility.UpdatedByUserId =
                _currentUserService.UserId;

            facility.UpdatedAtUtc =
                now;

            facility.ConcurrencyToken =
                Guid.NewGuid();

            _facilityRepository.Update(facility);
        }

        try
        {
            await _facilityRepository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "One or more facilities changed while " +
                "maturity processing was running.");
        }

        foreach (var facility in facilities)
        {
            var totalOutstanding =
                facility.OutstandingPrincipalAmount +
                facility.AccruedInterestAmount;

            var alertCreated = false;

            if (totalOutstanding > 0)
            {
                var alertExists =
                    await _alertRepository.OpenAlertExists(
                        TreasuryAlertTypes
                            .CreditFacilityDebtOverdue,
                        AuditEntityTypes.CreditFacility,
                        facility.Id,
                        facility.Reference);

                if (!alertExists)
                {
                    await _alertService.Create(
                        new CreateTreasuryAlertDto
                        {
                            AlertType =
                                TreasuryAlertTypes
                                    .CreditFacilityDebtOverdue,

                            Severity =
                                TreasuryAlertSeverities.Critical,

                            Title =
                                $"Credit facility {facility.Reference} has matured with outstanding debt",

                            Message =
                                $"Facility {facility.Reference} " +
                                $"matured with outstanding debt " +
                                $"of {totalOutstanding:N2} " +
                                $"{facility.Currency}.",

                            AccountId =
                                facility.SettlementAccountId,

                            Currency =
                                facility.Currency,

                            SourceModule =
                                "Credit Facilities",

                            SourceEntityType =
                                AuditEntityTypes.CreditFacility,

                            SourceEntityId =
                                facility.Id,

                            SourceReference =
                                facility.Reference,

                            Metadata =
                                new
                                {
                                    facility.FacilityName,
                                    facility.LenderName,
                                    facility.MaturityDateUtc,
                                    facility
                                        .OutstandingPrincipalAmount,
                                    facility
                                        .AccruedInterestAmount,
                                    TotalOutstandingAmount =
                                        totalOutstanding
                                }
                        });

                    alertCreated = true;

                    result.OverdueAlertsCreated += 1;
                }
            }

            result.Items.Add(
                new CreditFacilityMaturityProcessingItemDto
                {
                    CreditFacilityId =
                        facility.Id,

                    FacilityReference =
                        facility.Reference,

                    Currency =
                        facility.Currency,

                    MaturityDateUtc =
                        facility.MaturityDateUtc,

                    OutstandingPrincipalAmount =
                        facility.OutstandingPrincipalAmount,

                    AccruedInterestAmount =
                        facility.AccruedInterestAmount,

                    TotalOutstandingAmount =
                        totalOutstanding,

                    OverdueAlertCreated =
                        alertCreated
                });

            await RecordAudit(
                facility,
                AuditActionTypes.Matured,
                $"Credit facility {facility.Reference} was marked as matured.",
                beforeValues[facility.Id],
                Snapshot(facility));
        }

        result.FacilitiesMatured =
            facilities.Count;

        return result;
    }

    private async Task<CreditFacility> GetFacility(
        Guid creditFacilityId)
    {
        if (creditFacilityId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Credit facility ID is required.");
        }

        var facility =
            await _facilityRepository
                .GetById(creditFacilityId);

        if (facility is null)
        {
            throw new ResourceNotFoundException(
                "Credit facility was not found.");
        }

        return facility;
    }

    private async Task SaveFacility(
        CreditFacility facility,
        string concurrencyMessage)
    {
        _facilityRepository.Update(facility);

        try
        {
            await _facilityRepository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                concurrencyMessage);
        }
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
                            "Credit Facility Lifecycle",

                        facility.Currency,

                        facility
                            .OutstandingPrincipalAmount,

                        facility
                            .AccruedInterestAmount
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
            facility.Status,
            facility.MaturityDateUtc,
            facility.OutstandingPrincipalAmount,
            facility.AccruedInterestAmount,
            facility.SuspendedByUserId,
            facility.SuspendedAtUtc,
            facility.SuspensionReason,
            facility.MaturedAtUtc,
            facility.ClosedByUserId,
            facility.ClosedAtUtc,
            facility.ClosureReason,
            facility.UpdatedAtUtc
        };
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

        var normalized =
            value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new BusinessRuleException(
                $"{fieldName} cannot exceed " +
                $"{maximumLength} characters.");
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

    private static CreditFacilityLifecycleResponseDto Map(
        CreditFacility facility)
    {
        return new CreditFacilityLifecycleResponseDto
        {
            Id =
                facility.Id,

            Reference =
                facility.Reference,

            FacilityName =
                facility.FacilityName,

            FacilityType =
                facility.FacilityType,

            LenderName =
                facility.LenderName,

            Currency =
                facility.Currency,

            Status =
                facility.Status,

            ApprovedLimitAmount =
                facility.ApprovedLimitAmount,

            OutstandingPrincipalAmount =
                facility.OutstandingPrincipalAmount,

            AccruedInterestAmount =
                facility.AccruedInterestAmount,

            TotalOutstandingAmount =
                facility.TotalOutstandingAmount,

            AvailableAmount =
                facility.AvailableAmount,

            MaturityDateUtc =
                facility.MaturityDateUtc,

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

            UpdatedAtUtc =
                facility.UpdatedAtUtc
        };
    }
}