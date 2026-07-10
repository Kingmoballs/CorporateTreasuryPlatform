using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Fx;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;


namespace Treasury.Infrastructure.Services;

public class FxRateService
    : IFxRateService
{
    private static readonly HashSet<string> AllowedSourceTypes =
    [
        FxRateSourceTypes.Manual,
        FxRateSourceTypes.CentralBank,
        FxRateSourceTypes.Bank,
        FxRateSourceTypes.Market,
        FxRateSourceTypes.Other
    ];

    private readonly IFxRateRepository _fxRateRepository;

    private readonly IAccountRepository _accountRepository;

    private readonly ICurrentUserService _currentUserService;

    private readonly IAuditLogService _auditLogService;

    public FxRateService(
        IFxRateRepository fxRateRepository,
        IAccountRepository accountRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _fxRateRepository =
            fxRateRepository;

        _accountRepository =
            accountRepository;

        _currentUserService =
            currentUserService;
        
        _auditLogService =
            auditLogService;
    }

    public async Task<FxRateResponseDto> Create(
        CreateFxRateDto dto)
    {
        ValidateCreateDto(dto);

        var fromCurrency =
            NormalizeCurrency(dto.FromCurrency);

        var toCurrency =
            NormalizeCurrency(dto.ToCurrency);

        EnsureDifferentCurrencies(
            fromCurrency,
            toCurrency);

        var rateDateUtc =
            NormalizeUtc(dto.RateDateUtc);

        var existingRate =
            await _fxRateRepository.RateExistsForDate(
                fromCurrency,
                toCurrency,
                rateDateUtc);

        if (existingRate)
        {
            throw new ConflictException(
                "An FX rate already exists for this " +
                "currency pair and rate date.");
        }

        var fxRate =
            new FxRate
            {
                Id =
                    Guid.NewGuid(),

                FromCurrency =
                    fromCurrency,

                ToCurrency =
                    toCurrency,

                Rate =
                    dto.Rate,

                RateDateUtc =
                    rateDateUtc,

                SourceType =
                    NormalizeSourceType(dto.SourceType),

                SourceReference =
                    NormalizeOptionalText(
                        dto.SourceReference),

                IsActive =
                    dto.IsActive,

                CreatedByUserId =
                    _currentUserService.UserId,

                CreatedAtUtc =
                    DateTime.UtcNow,

                UpdatedAtUtc =
                    DateTime.UtcNow
            };

        await _fxRateRepository.Add(fxRate);

        await _fxRateRepository.SaveChanges();

        var savedRate =
            await _fxRateRepository.GetById(fxRate.Id);

        var response =
            MapRate(savedRate ?? fxRate);

        await RecordFxRateCreatedAudit(response);

        return response;
    }

    public async Task<FxRateResponseDto> Update(
        Guid id,
        UpdateFxRateDto dto)
    {
        if (dto.Rate <= 0)
        {
            throw new BusinessRuleException(
                "FX rate must be greater than zero.");
        }

        var fxRate =
            await _fxRateRepository.GetById(id);

        if (fxRate is null)
        {
            throw new ResourceNotFoundException(
                "FX rate not found.");
        }

        var beforeValues =
            SnapshotFxRate(fxRate);

        fxRate.Rate =
            dto.Rate;

        fxRate.SourceType =
            NormalizeSourceType(dto.SourceType);

        fxRate.SourceReference =
            NormalizeOptionalText(
                dto.SourceReference);

        fxRate.IsActive =
            dto.IsActive;

        fxRate.UpdatedAtUtc =
            DateTime.UtcNow;

        fxRate.ConcurrencyToken =
            Guid.NewGuid();

        _fxRateRepository.Update(fxRate);

        await _fxRateRepository.SaveChanges();

        var savedRate =
            await _fxRateRepository.GetById(id);

        var response =
            MapRate(savedRate ?? fxRate);

        await RecordFxRateUpdatedAudit(
            beforeValues,
            response);

        return response;
    }

    public async Task<FxRateResponseDto> GetById(
        Guid id)
    {
        var fxRate =
            await _fxRateRepository.GetById(id);

        if (fxRate is null)
        {
            throw new ResourceNotFoundException(
                "FX rate not found.");
        }

        return MapRate(fxRate);
    }

    public async Task<FxRateResponseDto> GetLatestRate(
        string fromCurrency,
        string toCurrency,
        DateTime? asOfUtc)
    {
        var conversion =
            await ResolveConversion(
                fromCurrency,
                toCurrency,
                asOfUtc);

        if (conversion.FxRate is null)
        {
            return new FxRateResponseDto
            {
                FromCurrency =
                    conversion.FromCurrency,

                ToCurrency =
                    conversion.ToCurrency,

                Rate =
                    1m,

                RateDateUtc =
                    conversion.AsOfUtc,

                SourceType =
                    FxRateSourceTypes.Manual,

                IsActive =
                    true
            };
        }

        return MapRate(conversion.FxRate);
    }

    public async Task<List<FxRateResponseDto>> GetRates(
        string? fromCurrency,
        string? toCurrency,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            NormalizeUtc(fromUtc.Value) >
            NormalizeUtc(toUtc.Value))
        {
            throw new BusinessRuleException(
                "Start date cannot be later than end date.");
        }

        var rates =
            await _fxRateRepository.GetRates(
                string.IsNullOrWhiteSpace(fromCurrency)
                    ? null
                    : NormalizeCurrency(fromCurrency),
                string.IsNullOrWhiteSpace(toCurrency)
                    ? null
                    : NormalizeCurrency(toCurrency),
                fromUtc.HasValue
                    ? NormalizeUtc(fromUtc.Value)
                    : null,
                toUtc.HasValue
                    ? NormalizeUtc(toUtc.Value)
                    : null);

        return rates
            .Select(MapRate)
            .ToList();
    }

    public async Task<CurrencyConversionResponseDto> ConvertAmount(
        decimal amount,
        string fromCurrency,
        string toCurrency,
        DateTime? asOfUtc)
    {
        if (amount < 0)
        {
            throw new BusinessRuleException(
                "Amount cannot be negative.");
        }

        var conversion =
            await ResolveConversion(
                fromCurrency,
                toCurrency,
                asOfUtc);

        return new CurrencyConversionResponseDto
        {
            Amount =
                amount,

            FromCurrency =
                conversion.FromCurrency,

            ToCurrency =
                conversion.ToCurrency,

            ConvertedAmount =
                Math.Round(
                    amount * conversion.EffectiveRate,
                    2),

            EffectiveRate =
                conversion.EffectiveRate,

            FxRateId =
                conversion.FxRate?.Id,

            FxRateDateUtc =
                conversion.FxRate?.RateDateUtc,

            UsedInverseRate =
                conversion.UsedInverseRate,

            AsOfUtc =
                conversion.AsOfUtc
        };
    }

    public async Task<ConsolidatedCashPositionDto>
        GetConsolidatedCashPosition(
            string baseCurrency,
            DateTime? asOfUtc)
    {
        var normalizedBaseCurrency =
            NormalizeCurrency(baseCurrency);

        var effectiveAsOfUtc =
            NormalizeNullableUtc(asOfUtc)
            ?? DateTime.UtcNow;

        var accounts =
            await _accountRepository.GetAll();

        var activeAccounts =
            accounts
                .Where(account =>
                    account.IsActive)
                .OrderBy(account =>
                    account.Currency)
                .ThenBy(account =>
                    account.Name)
                .ToList();

        var accountDtos =
            new List<ConsolidatedCashPositionAccountDto>();

        foreach (var account in activeAccounts)
        {
            var conversion =
                await ResolveConversion(
                    account.Currency,
                    normalizedBaseCurrency,
                    effectiveAsOfUtc);

            accountDtos.Add(
                new ConsolidatedCashPositionAccountDto
                {
                    AccountId =
                        account.Id,

                    AccountName =
                        account.Name,

                    AccountNumber =
                        account.AccountNumber,

                    AccountType =
                        account.AccountType?.Name,

                    Currency =
                        account.Currency,

                    Balance =
                        account.Balance,

                    AvailableBalance =
                        account.AvailableBalance,

                    ReservedBalance =
                        account.ReservedBalance,

                    EffectiveRate =
                        conversion.EffectiveRate,

                    FxRateId =
                        conversion.FxRate?.Id,

                    FxRateDateUtc =
                        conversion.FxRate?.RateDateUtc,

                    UsedInverseRate =
                        conversion.UsedInverseRate,

                    ConvertedBalance =
                        Math.Round(
                            account.Balance *
                            conversion.EffectiveRate,
                            2),

                    ConvertedAvailableBalance =
                        Math.Round(
                            account.AvailableBalance *
                            conversion.EffectiveRate,
                            2),

                    ConvertedReservedBalance =
                        Math.Round(
                            account.ReservedBalance *
                            conversion.EffectiveRate,
                            2)
                });
        }

        return new ConsolidatedCashPositionDto
        {
            BaseCurrency =
                normalizedBaseCurrency,

            AsOfUtc =
                effectiveAsOfUtc,

            GeneratedAtUtc =
                DateTime.UtcNow,

            AccountCount =
                accountDtos.Count,

            TotalBalanceInBaseCurrency =
                accountDtos.Sum(account =>
                    account.ConvertedBalance),

            TotalAvailableBalanceInBaseCurrency =
                accountDtos.Sum(account =>
                    account.ConvertedAvailableBalance),

            TotalReservedBalanceInBaseCurrency =
                accountDtos.Sum(account =>
                    account.ConvertedReservedBalance),

            Accounts =
                accountDtos
        };
    }

    public async Task<CurrencyExposureReportDto>
        GetCurrencyExposureReport(
            string baseCurrency,
            DateTime? asOfUtc)
    {
        var consolidated =
            await GetConsolidatedCashPosition(
                baseCurrency,
                asOfUtc);

        var totalAvailable =
            consolidated
                .TotalAvailableBalanceInBaseCurrency;

        var exposures =
            consolidated.Accounts
                .GroupBy(account =>
                    account.Currency)
                .Select(group =>
                {
                    var firstAccount =
                        group.First();

                    var totalBalance =
                        group.Sum(account =>
                            account.Balance);

                    var totalAvailableBalance =
                        group.Sum(account =>
                            account.AvailableBalance);

                    var totalReservedBalance =
                        group.Sum(account =>
                            account.ReservedBalance);

                    var totalBalanceInBase =
                        group.Sum(account =>
                            account.ConvertedBalance);

                    var totalAvailableInBase =
                        group.Sum(account =>
                            account.ConvertedAvailableBalance);

                    var totalReservedInBase =
                        group.Sum(account =>
                            account.ConvertedReservedBalance);

                    return new CurrencyExposureDto
                    {
                        Currency =
                            group.Key,

                        AccountCount =
                            group.Count(),

                        TotalBalance =
                            totalBalance,

                        TotalAvailableBalance =
                            totalAvailableBalance,

                        TotalReservedBalance =
                            totalReservedBalance,

                        EffectiveRateToBaseCurrency =
                            firstAccount.EffectiveRate,

                        FxRateId =
                            firstAccount.FxRateId,

                        FxRateDateUtc =
                            firstAccount.FxRateDateUtc,

                        UsedInverseRate =
                            firstAccount.UsedInverseRate,

                        TotalBalanceInBaseCurrency =
                            totalBalanceInBase,

                        TotalAvailableBalanceInBaseCurrency =
                            totalAvailableInBase,

                        TotalReservedBalanceInBaseCurrency =
                            totalReservedInBase,

                        PercentageOfTotalAvailableLiquidity =
                            totalAvailable == 0
                                ? 0m
                                : Math.Round(
                                    totalAvailableInBase *
                                    100m /
                                    totalAvailable,
                                    2)
                    };
                })
                .OrderByDescending(exposure =>
                    exposure.TotalAvailableBalanceInBaseCurrency)
                .ToList();

        return new CurrencyExposureReportDto
        {
            BaseCurrency =
                consolidated.BaseCurrency,

            AsOfUtc =
                consolidated.AsOfUtc,

            GeneratedAtUtc =
                DateTime.UtcNow,

            TotalAvailableLiquidityInBaseCurrency =
                totalAvailable,

            Exposures =
                exposures
        };
    }

    private async Task<FxConversion> ResolveConversion(
        string fromCurrency,
        string toCurrency,
        DateTime? asOfUtc)
    {
        var normalizedFrom =
            NormalizeCurrency(fromCurrency);

        var normalizedTo =
            NormalizeCurrency(toCurrency);

        var effectiveAsOfUtc =
            NormalizeNullableUtc(asOfUtc)
            ?? DateTime.UtcNow;

        if (normalizedFrom == normalizedTo)
        {
            return new FxConversion(
                normalizedFrom,
                normalizedTo,
                effectiveAsOfUtc,
                EffectiveRate: 1m,
                FxRate: null,
                UsedInverseRate: false);
        }

        var directRate =
            await _fxRateRepository.GetLatestRate(
                normalizedFrom,
                normalizedTo,
                effectiveAsOfUtc);

        if (directRate is not null)
        {
            return new FxConversion(
                normalizedFrom,
                normalizedTo,
                effectiveAsOfUtc,
                directRate.Rate,
                directRate,
                UsedInverseRate: false);
        }

        var inverseRate =
            await _fxRateRepository.GetLatestRate(
                normalizedTo,
                normalizedFrom,
                effectiveAsOfUtc);

        if (inverseRate is not null)
        {
            return new FxConversion(
                normalizedFrom,
                normalizedTo,
                effectiveAsOfUtc,
                Math.Round(
                    1m / inverseRate.Rate,
                    10),
                inverseRate,
                UsedInverseRate: true);
        }

        throw new ResourceNotFoundException(
            $"No FX rate found for {normalizedFrom} " +
            $"to {normalizedTo} as of " +
            $"{effectiveAsOfUtc:yyyy-MM-dd}.");
    }

    private static void ValidateCreateDto(
        CreateFxRateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FromCurrency))
        {
            throw new BusinessRuleException(
                "From currency is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.ToCurrency))
        {
            throw new BusinessRuleException(
                "To currency is required.");
        }

        if (dto.Rate <= 0)
        {
            throw new BusinessRuleException(
                "FX rate must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(dto.SourceType))
        {
            throw new BusinessRuleException(
                "FX rate source type is required.");
        }
    }

    private static void EnsureDifferentCurrencies(
        string fromCurrency,
        string toCurrency)
    {
        if (fromCurrency == toCurrency)
        {
            throw new BusinessRuleException(
                "From currency and to currency must be different.");
        }
    }

    private static string NormalizeCurrency(
        string currency)
    {
        var normalized =
            currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3)
        {
            throw new BusinessRuleException(
                "Currency must be a 3-letter code.");
        }

        return normalized;
    }

    private static string NormalizeSourceType(
        string sourceType)
    {
        var normalized =
            sourceType.Trim();

        var match =
            AllowedSourceTypes.FirstOrDefault(value =>
                string.Equals(
                    value,
                    normalized,
                    StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new BusinessRuleException(
                "Invalid FX rate source type.");
        }

        return match;
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static DateTime NormalizeUtc(
        DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Local)
        {
            return value.ToUniversalTime();
        }

        return DateTime.SpecifyKind(
            value,
            DateTimeKind.Utc);
    }

    private static DateTime? NormalizeNullableUtc(
        DateTime? value)
    {
        return value.HasValue
            ? NormalizeUtc(value.Value)
            : null;
    }

    private static FxRateResponseDto MapRate(
        FxRate rate)
    {
        return new FxRateResponseDto
        {
            Id =
                rate.Id,

            FromCurrency =
                rate.FromCurrency,

            ToCurrency =
                rate.ToCurrency,

            Rate =
                rate.Rate,

            RateDateUtc =
                rate.RateDateUtc,

            SourceType =
                rate.SourceType,

            SourceReference =
                rate.SourceReference,

            IsActive =
                rate.IsActive,

            CreatedByUserId =
                rate.CreatedByUserId,

            CreatedAtUtc =
                rate.CreatedAtUtc,

            UpdatedAtUtc =
                rate.UpdatedAtUtc
        };
    }

    private async Task RecordFxRateCreatedAudit(
        FxRateResponseDto rate)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Created,

                EntityType =
                    AuditEntityTypes.FxRate,

                EntityId =
                    rate.Id,

                EntityReference =
                    $"{rate.FromCurrency}/{rate.ToCurrency}",

                Summary =
                    $"FX rate {rate.FromCurrency}/{rate.ToCurrency} " +
                    $"was created for {rate.RateDateUtc:yyyy-MM-dd}.",

                AfterValues =
                    SnapshotFxRate(rate),

                Metadata =
                    new
                    {
                        Module = "FX Rates"
                    }
            });
    }

    private async Task RecordFxRateUpdatedAudit(
        object beforeValues,
        FxRateResponseDto rate)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Updated,

                EntityType =
                    AuditEntityTypes.FxRate,

                EntityId =
                    rate.Id,

                EntityReference =
                    $"{rate.FromCurrency}/{rate.ToCurrency}",

                Summary =
                    $"FX rate {rate.FromCurrency}/{rate.ToCurrency} " +
                    $"was updated.",

                BeforeValues =
                    beforeValues,

                AfterValues =
                    SnapshotFxRate(rate),

                Metadata =
                    new
                    {
                        Module = "FX Rates"
                    }
            });
    }

    private static object SnapshotFxRate(
        FxRate rate)
    {
        return new
        {
            rate.Id,
            rate.FromCurrency,
            rate.ToCurrency,
            rate.Rate,
            rate.RateDateUtc,
            rate.SourceType,
            rate.SourceReference,
            rate.IsActive,
            rate.CreatedByUserId,
            rate.CreatedAtUtc,
            rate.UpdatedAtUtc
        };
    }

    private static object SnapshotFxRate(
        FxRateResponseDto rate)
    {
        return new
        {
            rate.Id,
            rate.FromCurrency,
            rate.ToCurrency,
            rate.Rate,
            rate.RateDateUtc,
            rate.SourceType,
            rate.SourceReference,
            rate.IsActive,
            rate.CreatedByUserId,
            rate.CreatedAtUtc,
            rate.UpdatedAtUtc
        };
    }

    private sealed record FxConversion(
        string FromCurrency,
        string ToCurrency,
        DateTime AsOfUtc,
        decimal EffectiveRate,
        FxRate? FxRate,
        bool UsedInverseRate);
}