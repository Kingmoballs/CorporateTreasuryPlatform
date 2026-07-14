using Treasury.Application.DTOs.TreasuryAlerts;
using Treasury.Application.DTOs.Exports;

namespace Treasury.Application.Interfaces;

public interface ITreasuryAlertService
{
    Task<TreasuryAlertResponseDto> Create(CreateTreasuryAlertDto dto);

    Task<PagedTreasuryAlertResponseDto> Search(TreasuryAlertQueryDto query);

    Task<TreasuryAlertSummaryDto> GetSummary(TreasuryAlertSummaryQueryDto query);

    Task<CsvExportDto> ExportCsv(
        TreasuryAlertQueryDto query,
        int maxRows = 5000);

    Task<TreasuryAlertResponseDto> Resolve(Guid id, string? note);

    Task<TreasuryAlertResponseDto> Dismiss(Guid id, string? note);
}