using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.Counterparties;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class CounterpartyService
    : ICounterpartyService
{
    private static readonly string[]
        AllowedCounterpartyTypes =
        {
            CounterpartyTypes.Bank,
            CounterpartyTypes
                .NonBankFinancialInstitution,
            CounterpartyTypes.Corporate,
            CounterpartyTypes.Government
        };

    private readonly ICounterpartyRepository
        _counterpartyRepository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuditLogService
        _auditLogService;

    public CounterpartyService(
        ICounterpartyRepository counterpartyRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _counterpartyRepository =
            counterpartyRepository;

        _currentUserService =
            currentUserService;

        _auditLogService =
            auditLogService;
    }

    public async Task<CounterpartyResponseDto>
        Create(CreateCounterpartyDto dto)
    {
        var code =
            NormalizeCode(dto.Code);

        if (await _counterpartyRepository
                .CodeExists(code))
        {
            throw new ConflictException(
                $"A counterparty with code " +
                $"{code} already exists.");
        }

        var now = DateTime.UtcNow;

        var counterparty =
            new Counterparty
            {
                Id =
                    Guid.NewGuid(),

                Code =
                    code,

                Name =
                    NormalizeRequiredText(
                        dto.Name,
                        "Counterparty name",
                        200),

                CounterpartyType =
                    NormalizeCounterpartyType(
                        dto.CounterpartyType),

                CountryCode =
                    NormalizeCountryCode(
                        dto.CountryCode),

                SwiftCode =
                    NormalizeSwiftCode(
                        dto.SwiftCode),

                CreditRating =
                    NormalizeOptionalText(
                        dto.CreditRating,
                        "Credit rating",
                        20),

                IsActive =
                    dto.IsActive,

                Notes =
                    NormalizeOptionalText(
                        dto.Notes,
                        "Notes",
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

        await _counterpartyRepository.Add(
            counterparty);

        try
        {
            await _counterpartyRepository
                .SaveChanges();
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The counterparty could not be created. " +
                "Its code may already be in use.");
        }

        await RecordCreatedAudit(counterparty);

        return Map(counterparty);
    }

    public async Task<CounterpartyResponseDto>
        GetById(Guid id)
    {
        var counterparty =
            await GetRequiredCounterparty(id);

        return Map(counterparty);
    }

    public async Task<PagedCounterpartyResponseDto>
        Search(CounterpartyQueryDto query)
    {
        var normalizedQuery =
            NormalizeQuery(query);

        var result =
            await _counterpartyRepository.Search(
                normalizedQuery);

        return new PagedCounterpartyResponseDto
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

    public async Task<CounterpartyResponseDto>
        Update(
            Guid id,
            UpdateCounterpartyDto dto)
    {
        var counterparty =
            await GetRequiredCounterparty(id);

        var beforeValues =
            Snapshot(counterparty);

        counterparty.Name =
            NormalizeRequiredText(
                dto.Name,
                "Counterparty name",
                200);

        counterparty.CounterpartyType =
            NormalizeCounterpartyType(
                dto.CounterpartyType);

        counterparty.CountryCode =
            NormalizeCountryCode(
                dto.CountryCode);

        counterparty.SwiftCode =
            NormalizeSwiftCode(
                dto.SwiftCode);

        counterparty.CreditRating =
            NormalizeOptionalText(
                dto.CreditRating,
                "Credit rating",
                20);

        counterparty.Notes =
            NormalizeOptionalText(
                dto.Notes,
                "Notes",
                1000);

        counterparty.UpdatedByUserId =
            _currentUserService.UserId;

        counterparty.UpdatedAtUtc =
            DateTime.UtcNow;

        counterparty.ConcurrencyToken =
            Guid.NewGuid();

        _counterpartyRepository.Update(
            counterparty);

        await SaveUpdatedCounterparty();

        await RecordUpdatedAudit(
            counterparty,
            beforeValues,
            "Counterparty details were updated.");

        return Map(counterparty);
    }

    public async Task<CounterpartyResponseDto>
        SetStatus(
            Guid id,
            bool isActive)
    {
        var counterparty =
            await GetRequiredCounterparty(id);

        if (counterparty.IsActive == isActive)
        {
            return Map(counterparty);
        }

        var beforeValues =
            Snapshot(counterparty);

        counterparty.IsActive =
            isActive;

        counterparty.UpdatedByUserId =
            _currentUserService.UserId;

        counterparty.UpdatedAtUtc =
            DateTime.UtcNow;

        counterparty.ConcurrencyToken =
            Guid.NewGuid();

        _counterpartyRepository.Update(
            counterparty);

        await SaveUpdatedCounterparty();

        await RecordUpdatedAudit(
            counterparty,
            beforeValues,
            isActive
                ? "Counterparty was activated."
                : "Counterparty was deactivated.");

        return Map(counterparty);
    }

    private async Task<Counterparty>
        GetRequiredCounterparty(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Counterparty ID is required.");
        }

        var counterparty =
            await _counterpartyRepository
                .GetById(id);

        if (counterparty is null)
        {
            throw new ResourceNotFoundException(
                "Counterparty was not found.");
        }

        return counterparty;
    }

    private async Task SaveUpdatedCounterparty()
    {
        try
        {
            await _counterpartyRepository
                .SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The counterparty was changed by " +
                "another request. Reload it and try again.");
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The counterparty update could not " +
                "be completed.");
        }
    }

    private async Task RecordCreatedAudit(
        Counterparty counterparty)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Created,

                EntityType =
                    AuditEntityTypes.Counterparty,

                EntityId =
                    counterparty.Id,

                EntityReference =
                    counterparty.Code,

                Summary =
                    $"Counterparty {counterparty.Code} " +
                    $"was created.",

                AfterValues =
                    Snapshot(counterparty),

                Metadata =
                    new
                    {
                        Module =
                            "Counterparty Management"
                    }
            });
    }

    private async Task RecordUpdatedAudit(
        Counterparty counterparty,
        object beforeValues,
        string summary)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Updated,

                EntityType =
                    AuditEntityTypes.Counterparty,

                EntityId =
                    counterparty.Id,

                EntityReference =
                    counterparty.Code,

                Summary =
                    summary,

                BeforeValues =
                    beforeValues,

                AfterValues =
                    Snapshot(counterparty),

                Metadata =
                    new
                    {
                        Module =
                            "Counterparty Management"
                    }
            });
    }

    private static CounterpartyQueryDto
        NormalizeQuery(
            CounterpartyQueryDto query)
    {
        var page =
            query.Page < 1
                ? 1
                : query.Page;

        var pageSize =
            query.PageSize < 1
                ? 50
                : Math.Min(
                    query.PageSize,
                    100);

        string? search = null;

        if (!string.IsNullOrWhiteSpace(
                query.Search))
        {
            search =
                query.Search.Trim();

            if (search.Length > 100)
            {
                throw new BusinessRuleException(
                    "Search text cannot exceed " +
                    "100 characters.");
            }
        }

        return new CounterpartyQueryDto
        {
            Search =
                search,

            CounterpartyType =
                string.IsNullOrWhiteSpace(
                    query.CounterpartyType)
                    ? null
                    : NormalizeCounterpartyType(
                        query.CounterpartyType),

            IsActive =
                query.IsActive,

            Page =
                page,

            PageSize =
                pageSize
        };
    }

    private static string NormalizeCode(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                "Counterparty code is required.");
        }

        var code =
            value.Trim().ToUpperInvariant();

        if (!Regex.IsMatch(
                code,
                "^[A-Z0-9][A-Z0-9-]{0,29}$"))
        {
            throw new BusinessRuleException(
                "Counterparty code can contain only " +
                "letters, numbers and hyphens, and " +
                "cannot exceed 30 characters.");
        }

        return code;
    }

    private static string
        NormalizeCounterpartyType(
            string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                "Counterparty type is required.");
        }

        var counterpartyType =
            AllowedCounterpartyTypes
                .FirstOrDefault(type =>
                    string.Equals(
                        type,
                        value.Trim(),
                        StringComparison.OrdinalIgnoreCase));

        if (counterpartyType is null)
        {
            throw new BusinessRuleException(
                "Counterparty type must be Bank, " +
                "NonBankFinancialInstitution, " +
                "Corporate or Government.");
        }

        return counterpartyType;
    }

    private static string NormalizeCountryCode(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                "Country code is required.");
        }

        var countryCode =
            value.Trim().ToUpperInvariant();

        if (!Regex.IsMatch(
                countryCode,
                "^[A-Z]{2}$"))
        {
            throw new BusinessRuleException(
                "Country code must contain exactly " +
                "two letters.");
        }

        return countryCode;
    }

    private static string? NormalizeSwiftCode(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var swiftCode =
            value.Trim().ToUpperInvariant();

        if (!Regex.IsMatch(
                swiftCode,
                "^[A-Z0-9]{8}([A-Z0-9]{3})?$"))
        {
            throw new BusinessRuleException(
                "SWIFT code must contain 8 or " +
                "11 letters and numbers.");
        }

        return swiftCode;
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
                $"{fieldName} cannot exceed " +
                $"{maximumLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(
        string? value,
        string fieldName,
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
                $"{fieldName} cannot exceed " +
                $"{maximumLength} characters.");
        }

        return normalized;
    }

    private static CounterpartyResponseDto Map(
        Counterparty counterparty)
    {
        return new CounterpartyResponseDto
        {
            Id =
                counterparty.Id,

            Code =
                counterparty.Code,

            Name =
                counterparty.Name,

            CounterpartyType =
                counterparty.CounterpartyType,

            CountryCode =
                counterparty.CountryCode,

            SwiftCode =
                counterparty.SwiftCode,

            CreditRating =
                counterparty.CreditRating,

            IsActive =
                counterparty.IsActive,

            Notes =
                counterparty.Notes,

            CreatedByUserId =
                counterparty.CreatedByUserId,

            UpdatedByUserId =
                counterparty.UpdatedByUserId,

            CreatedAtUtc =
                counterparty.CreatedAtUtc,

            UpdatedAtUtc =
                counterparty.UpdatedAtUtc
        };
    }

    private static object Snapshot(
        Counterparty counterparty)
    {
        return new
        {
            counterparty.Id,
            counterparty.Code,
            counterparty.Name,
            counterparty.CounterpartyType,
            counterparty.CountryCode,
            counterparty.SwiftCode,
            counterparty.CreditRating,
            counterparty.IsActive,
            counterparty.Notes,
            counterparty.CreatedByUserId,
            counterparty.UpdatedByUserId,
            counterparty.CreatedAtUtc,
            counterparty.UpdatedAtUtc
        };
    }
}