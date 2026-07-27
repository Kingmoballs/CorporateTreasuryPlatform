using Treasury.Application.DTOs.CashFlowForecasts;
using Treasury.Application.DTOs.Exports;

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
        DateTime toUtc,
        Guid? legalEntityId = null,
        Guid? businessUnitId = null);

    Task<CashFlowForecastItemResponseDto> Cancel(
        Guid id);

    Task<CashFlowForecastReportDto> GetForecastReport(
        Guid? accountId,
        string? currency,
        DateTime fromUtc,
        DateTime toUtc,
        decimal minimumLiquidityThreshold,
        Guid? legalEntityId = null,
        Guid? businessUnitId = null);

    Task<CashFlowForecastVarianceReportDto> GetVarianceReport(
        CashFlowForecastVarianceQueryDto query);

    Task<CsvExportDto> ExportVarianceReportCsv(
        CashFlowForecastVarianceQueryDto query);

    Task<CashFlowForecastItemResponseDto> Realize(
        Guid id,
        Guid treasuryTransactionId);
}
