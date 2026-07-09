using Treasury.Application.DTOs.CashFlowForecasts;

namespace Treasury.Application.Interfaces;

public interface ICashFlowForecastService
{
    Task<CashFlowForecastItemResponseDto> Create(
        CreateCashFlowForecastItemDto dto);

    Task<CashFlowForecastItemResponseDto> GetById(
        Guid id);

    Task<List<CashFlowForecastItemResponseDto>> GetActive(
        Guid? accountId,
        string? currency,
        DateTime fromUtc,
        DateTime toUtc);

    Task<CashFlowForecastItemResponseDto> Cancel(
        Guid id);

    Task<CashFlowForecastReportDto> GetForecastReport(
        Guid? accountId,
        string? currency,
        DateTime fromUtc,
        DateTime toUtc,
        decimal minimumLiquidityThreshold);

    Task<CashFlowForecastItemResponseDto> Realize(
        Guid id,
        Guid treasuryTransactionId);
}