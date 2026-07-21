using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.InvestmentLimits;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class InvestmentLimitService
    : IInvestmentLimitService
{
    private readonly IInvestmentLimitRepository
        _investmentLimitRepository;

    private readonly ICounterpartyRepository
        _counterpartyRepository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuditLogService
        _auditLogService;

    public InvestmentLimitService(
        IInvestmentLimitRepository
            investmentLimitRepository,
        ICounterpartyRepository
            counterpartyRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _investmentLimitRepository =
            investmentLimitRepository;

        _counterpartyRepository =
            counterpartyRepository;

        _currentUserService =
            currentUserService;

        _auditLogService =
            auditLogService;
    }

    public async Task<InvestmentLimitResponseDto>
        Create(CreateInvestmentLimitDto dto)
    {
        if (dto.CounterpartyId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Counterparty ID is required.");
        }

        var counterparty =
            await _counterpartyRepository
                .GetById(dto.CounterpartyId);

        if (counterparty is null)
        {
            throw new ResourceNotFoundException(
                "Counterparty was not found.");
        }

        if (!counterparty.IsActive)
        {
            throw new ConflictException(
                "An investment limit cannot be created " +
                "for an inactive counterparty.");
        }

        var currency =
            NormalizeCurrency(dto.Currency);

        var investmentType =
            NormalizeInvestmentType(
                dto.InvestmentType);

        var maximumExposure =
            NormalizeMaximumExposure(
                dto.MaximumExposureAmount);

        var warningThreshold =
            NormalizeWarningThreshold(
                dto.WarningThresholdPercentage);

        var effectiveFromUtc =
            NormalizeRequiredDate(
                dto.EffectiveFromUtc,
                "Effective-from date");

        var effectiveToUtc =
            NormalizeOptionalDate(
                dto.EffectiveToUtc);

        ValidateEffectiveDates(
            effectiveFromUtc,
            effectiveToUtc);

        if (dto.IsActive &&
            await HasOverlap(
                counterparty.Id,
                currency,
                investmentType,
                effectiveFromUtc,
                effectiveToUtc,
                excludedLimitId: null))
        {
            throw new ConflictException(
                "An active investment limit already " +
                "overlaps this counterparty, currency " +
                "and investment-type period.");
        }

        var now =
            DateTime.UtcNow;

        var investmentLimit =
            new InvestmentLimit
            {
                Id =
                    Guid.NewGuid(),

                CounterpartyId =
                    counterparty.Id,

                Counterparty =
                    counterparty,

                Currency =
                    currency,

                InvestmentType =
                    investmentType,

                MaximumExposureAmount =
                    maximumExposure,

                WarningThresholdPercentage =
                    warningThreshold,

                EffectiveFromUtc =
                    effectiveFromUtc,

                EffectiveToUtc =
                    effectiveToUtc,

                IsActive =
                    dto.IsActive,

                Notes =
                    NormalizeOptionalText(
                        dto.Notes,
                        1000),

                CreatedByUserId =
                    _currentUserService.UserId,

                UpdatedByUserId =
                    _currentUserService.UserId,

                CreatedAtUtc =
                    now,

                UpdatedAtUtc =
                    now,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        await _investmentLimitRepository.Add(
            investmentLimit);

        try
        {
            await _investmentLimitRepository
                .SaveChanges();
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The investment limit could not be " +
                "created. A conflicting limit may " +
                "already exist.");
        }

        await RecordCreatedAudit(
            investmentLimit);

        return Map(investmentLimit);
    }

    public async Task<InvestmentLimitResponseDto>
        GetById(Guid id)
    {
        var investmentLimit =
            await GetRequiredLimit(id);

        return Map(investmentLimit);
    }

    public async Task<PagedInvestmentLimitResponseDto>
        Search(InvestmentLimitQueryDto query)
    {
        var normalizedQuery =
            NormalizeQuery(query);

        var result =
            await _investmentLimitRepository
                .Search(normalizedQuery);

        return new PagedInvestmentLimitResponseDto
        {
            Page =
                normalizedQuery.Page,

            PageSize =
                normalizedQuery.PageSize,

            TotalCount =
                result.TotalCount,

            TotalPages =
                result.TotalCount == 0
                    ? 0
                    : (int)Math.Ceiling(
                        result.TotalCount /
                        (double)normalizedQuery
                            .PageSize),

            Items =
                result.Items
                    .Select(Map)
                    .ToList()
        };
    }

    public async Task<InvestmentLimitResponseDto>
        Update(
            Guid id,
            UpdateInvestmentLimitDto dto)
    {
        var investmentLimit =
            await GetRequiredLimit(id);

        var maximumExposure =
            NormalizeMaximumExposure(
                dto.MaximumExposureAmount);

        var warningThreshold =
            NormalizeWarningThreshold(
                dto.WarningThresholdPercentage);

        var effectiveFromUtc =
            NormalizeRequiredDate(
                dto.EffectiveFromUtc,
                "Effective-from date");

        var effectiveToUtc =
            NormalizeOptionalDate(
                dto.EffectiveToUtc);

        ValidateEffectiveDates(
            effectiveFromUtc,
            effectiveToUtc);

        if (investmentLimit.IsActive &&
            await HasOverlap(
                investmentLimit.CounterpartyId,
                investmentLimit.Currency,
                investmentLimit.InvestmentType,
                effectiveFromUtc,
                effectiveToUtc,
                investmentLimit.Id))
        {
            throw new ConflictException(
                "The updated period overlaps another " +
                "active investment limit with the " +
                "same scope.");
        }

        var beforeValues =
            Snapshot(investmentLimit);

        investmentLimit.MaximumExposureAmount =
            maximumExposure;

        investmentLimit.WarningThresholdPercentage =
            warningThreshold;

        investmentLimit.EffectiveFromUtc =
            effectiveFromUtc;

        investmentLimit.EffectiveToUtc =
            effectiveToUtc;

        investmentLimit.Notes =
            NormalizeOptionalText(
                dto.Notes,
                1000);

        investmentLimit.UpdatedByUserId =
            _currentUserService.UserId;

        investmentLimit.UpdatedAtUtc =
            DateTime.UtcNow;

        investmentLimit.ConcurrencyToken =
            Guid.NewGuid();

        _investmentLimitRepository.Update(
            investmentLimit);

        await SaveUpdatedLimit();

        await RecordUpdatedAudit(
            investmentLimit,
            beforeValues,
            "Investment limit details were updated.");

        return Map(investmentLimit);
    }

    public async Task<InvestmentLimitResponseDto>
        SetStatus(
            Guid id,
            bool isActive)
    {
        var investmentLimit =
            await GetRequiredLimit(id);

        if (investmentLimit.IsActive == isActive)
        {
            return Map(investmentLimit);
        }

        if (isActive)
        {
            if (investmentLimit.Counterparty is null ||
                !investmentLimit.Counterparty.IsActive)
            {
                throw new ConflictException(
                    "The limit cannot be activated " +
                    "because its counterparty is inactive.");
            }

            if (await HasOverlap(
                    investmentLimit.CounterpartyId,
                    investmentLimit.Currency,
                    investmentLimit.InvestmentType,
                    investmentLimit.EffectiveFromUtc,
                    investmentLimit.EffectiveToUtc,
                    investmentLimit.Id))
            {
                throw new ConflictException(
                    "The limit cannot be activated " +
                    "because its period overlaps another " +
                    "active limit with the same scope.");
            }
        }

        var beforeValues =
            Snapshot(investmentLimit);

        investmentLimit.IsActive =
            isActive;

        investmentLimit.UpdatedByUserId =
            _currentUserService.UserId;

        investmentLimit.UpdatedAtUtc =
            DateTime.UtcNow;

        investmentLimit.ConcurrencyToken =
            Guid.NewGuid();

        _investmentLimitRepository.Update(
            investmentLimit);

        await SaveUpdatedLimit();

        await RecordUpdatedAudit(
            investmentLimit,
            beforeValues,
            isActive
                ? "Investment limit was activated."
                : "Investment limit was deactivated.");

        return Map(investmentLimit);
    }

    private async Task<InvestmentLimit>
        GetRequiredLimit(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Investment limit ID is required.");
        }

        var investmentLimit =
            await _investmentLimitRepository
                .GetById(id);

        if (investmentLimit is null)
        {
            throw new ResourceNotFoundException(
                "Investment limit was not found.");
        }

        return investmentLimit;
    }

    private async Task<bool> HasOverlap(
        Guid counterpartyId,
        string currency,
        string investmentType,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        Guid? excludedLimitId)
    {
        return await _investmentLimitRepository
            .HasOverlappingActiveLimit(
                counterpartyId,
                currency,
                investmentType,
                effectiveFromUtc,
                effectiveToUtc,
                excludedLimitId);
    }

    private async Task SaveUpdatedLimit()
    {
        try
        {
            await _investmentLimitRepository
                .SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The investment limit was changed by " +
                "another request. Reload it and try again.");
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The investment limit update could not " +
                "be completed.");
        }
    }

    private async Task RecordCreatedAudit(
        InvestmentLimit investmentLimit)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Created,

                EntityType =
                    AuditEntityTypes.InvestmentLimit,

                EntityId =
                    investmentLimit.Id,

                EntityReference =
                    BuildReference(investmentLimit),

                Summary =
                    $"Investment limit for " +
                    $"{investmentLimit.Counterparty.Code} " +
                    $"was created.",

                AfterValues =
                    Snapshot(investmentLimit),

                Metadata =
                    new
                    {
                        Module =
                            "Investment Limits"
                    }
            });
    }

    private async Task RecordUpdatedAudit(
        InvestmentLimit investmentLimit,
        object beforeValues,
        string summary)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Updated,

                EntityType =
                    AuditEntityTypes.InvestmentLimit,

                EntityId =
                    investmentLimit.Id,

                EntityReference =
                    BuildReference(investmentLimit),

                Summary =
                    summary,

                BeforeValues =
                    beforeValues,

                AfterValues =
                    Snapshot(investmentLimit),

                Metadata =
                    new
                    {
                        Module =
                            "Investment Limits"
                    }
            });
    }

    private static InvestmentLimitQueryDto
        NormalizeQuery(
            InvestmentLimitQueryDto query)
    {
        if (query.CounterpartyId.HasValue &&
            query.CounterpartyId.Value == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Counterparty ID is invalid.");
        }

        return new InvestmentLimitQueryDto
        {
            CounterpartyId =
                query.CounterpartyId,

            Currency =
                string.IsNullOrWhiteSpace(
                    query.Currency)
                    ? null
                    : NormalizeCurrency(
                        query.Currency),

            InvestmentType =
                string.IsNullOrWhiteSpace(
                    query.InvestmentType)
                    ? null
                    : NormalizeInvestmentType(
                        query.InvestmentType),

            IsActive =
                query.IsActive,

            AsOfUtc =
                query.AsOfUtc.HasValue
                    ? NormalizeUtc(
                        query.AsOfUtc.Value)
                    : null,

            Page =
                query.Page < 1
                    ? 1
                    : query.Page,

            PageSize =
                query.PageSize < 1
                    ? 50
                    : Math.Min(
                        query.PageSize,
                        100)
        };
    }

    private static decimal
        NormalizeMaximumExposure(
            decimal value)
    {
        var rounded =
            Math.Round(
                value,
                2,
                MidpointRounding.AwayFromZero);

        if (rounded <= 0)
        {
            throw new BusinessRuleException(
                "Maximum exposure amount must be " +
                "greater than zero.");
        }

        return rounded;
    }

    private static decimal
        NormalizeWarningThreshold(
            decimal value)
    {
        var rounded =
            Math.Round(
                value,
                2,
                MidpointRounding.AwayFromZero);

        if (rounded <= 0 ||
            rounded > 100)
        {
            throw new BusinessRuleException(
                "Warning threshold percentage must be " +
                "greater than zero and not greater " +
                "than 100.");
        }

        return rounded;
    }

    private static string NormalizeCurrency(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                "Currency is required.");
        }

        var currency =
            value.Trim().ToUpperInvariant();

        if (!Regex.IsMatch(
                currency,
                "^[A-Z]{3}$"))
        {
            throw new BusinessRuleException(
                "Currency must contain exactly " +
                "three letters.");
        }

        return currency;
    }

    private static string NormalizeInvestmentType(
        string? value)
    {
        if (string.Equals(
                value?.Trim(),
                InvestmentLimitScopes
                    .AllInvestmentTypes,
                StringComparison.OrdinalIgnoreCase))
        {
            return InvestmentLimitScopes
                .AllInvestmentTypes;
        }

        if (string.Equals(
                value?.Trim(),
                InvestmentPlacementTypes
                    .FixedDeposit,
                StringComparison.OrdinalIgnoreCase))
        {
            return InvestmentPlacementTypes
                .FixedDeposit;
        }

        throw new BusinessRuleException(
            "Investment type must be All or " +
            "FixedDeposit.");
    }

    private static DateTime NormalizeRequiredDate(
        DateTime value,
        string fieldName)
    {
        if (value == default)
        {
            throw new BusinessRuleException(
                $"{fieldName} is required.");
        }

        return NormalizeUtc(value);
    }

    private static DateTime? NormalizeOptionalDate(
        DateTime? value)
    {
        return value.HasValue
            ? NormalizeUtc(value.Value)
            : null;
    }

    private static void ValidateEffectiveDates(
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc)
    {
        if (effectiveToUtc.HasValue &&
            effectiveToUtc.Value <=
                effectiveFromUtc)
        {
            throw new BusinessRuleException(
                "Effective-to date must be later " +
                "than the effective-from date.");
        }
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
                $"Value cannot exceed {maximumLength} " +
                "characters.");
        }

        return normalized;
    }

    private static string BuildReference(
        InvestmentLimit investmentLimit)
    {
        return
            $"{investmentLimit.Counterparty.Code}/" +
            $"{investmentLimit.Currency}/" +
            $"{investmentLimit.InvestmentType}";
    }

    private static InvestmentLimitResponseDto Map(
        InvestmentLimit investmentLimit)
    {
        return new InvestmentLimitResponseDto
        {
            Id =
                investmentLimit.Id,

            CounterpartyId =
                investmentLimit.CounterpartyId,

            CounterpartyCode =
                investmentLimit.Counterparty?.Code ??
                string.Empty,

            CounterpartyName =
                investmentLimit.Counterparty?.Name ??
                string.Empty,

            Currency =
                investmentLimit.Currency,

            InvestmentType =
                investmentLimit.InvestmentType,

            MaximumExposureAmount =
                investmentLimit
                    .MaximumExposureAmount,

            WarningThresholdPercentage =
                investmentLimit
                    .WarningThresholdPercentage,

            EffectiveFromUtc =
                investmentLimit.EffectiveFromUtc,

            EffectiveToUtc =
                investmentLimit.EffectiveToUtc,

            IsActive =
                investmentLimit.IsActive,

            Notes =
                investmentLimit.Notes,

            CreatedByUserId =
                investmentLimit.CreatedByUserId,

            UpdatedByUserId =
                investmentLimit.UpdatedByUserId,

            CreatedAtUtc =
                investmentLimit.CreatedAtUtc,

            UpdatedAtUtc =
                investmentLimit.UpdatedAtUtc
        };
    }

    private static object Snapshot(
        InvestmentLimit investmentLimit)
    {
        return new
        {
            investmentLimit.Id,
            investmentLimit.CounterpartyId,
            CounterpartyCode =
                investmentLimit.Counterparty?.Code,
            investmentLimit.Currency,
            investmentLimit.InvestmentType,
            investmentLimit.MaximumExposureAmount,
            investmentLimit.WarningThresholdPercentage,
            investmentLimit.EffectiveFromUtc,
            investmentLimit.EffectiveToUtc,
            investmentLimit.IsActive,
            investmentLimit.Notes,
            investmentLimit.CreatedByUserId,
            investmentLimit.UpdatedByUserId,
            investmentLimit.CreatedAtUtc,
            investmentLimit.UpdatedAtUtc
        };
    }
}