using Treasury.Application.DTOs.TreasuryAlerts;

namespace Treasury.Application.Interfaces;

public interface ITreasuryAlertService
{
    Task<TreasuryAlertResponseDto> Create(CreateTreasuryAlertDto dto);

    Task<PagedTreasuryAlertResponseDto> Search(TreasuryAlertQueryDto query);

    Task<TreasuryAlertSummaryDto> GetSummary(TreasuryAlertSummaryQueryDto query);

    Task<TreasuryAlertResponseDto> Resolve(Guid id, string? note);

    Task<TreasuryAlertResponseDto> Dismiss(Guid id, string? note);
}