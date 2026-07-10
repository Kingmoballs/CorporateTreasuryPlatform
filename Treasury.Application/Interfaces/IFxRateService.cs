using Treasury.Application.DTOs.Fx;

namespace Treasury.Application.Interfaces;

public interface IFxRateService
{
    Task<FxRateResponseDto> Create(
        CreateFxRateDto dto);

    Task<FxRateResponseDto> Update(
        Guid id,
        UpdateFxRateDto dto);

    Task<FxRateResponseDto> GetById(
        Guid id);

    Task<FxRateResponseDto> GetLatestRate(
        string fromCurrency,
        string toCurrency,
        DateTime? asOfUtc);

    Task<List<FxRateResponseDto>> GetRates(
        string? fromCurrency,
        string? toCurrency,
        DateTime? fromUtc,
        DateTime? toUtc);

    Task<CurrencyConversionResponseDto> ConvertAmount(
        decimal amount,
        string fromCurrency,
        string toCurrency,
        DateTime? asOfUtc);

    Task<ConsolidatedCashPositionDto> GetConsolidatedCashPosition(
        string baseCurrency,
        DateTime? asOfUtc);

    Task<CurrencyExposureReportDto> GetCurrencyExposureReport(
        string baseCurrency,
        DateTime? asOfUtc);
}