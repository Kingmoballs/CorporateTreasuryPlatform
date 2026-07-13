using Treasury.Application.DTOs.TreasuryAlerts;

namespace Treasury.Application.Interfaces;

public interface ITreasuryAlertMonitoringService
{
    Task<TreasuryAlertScanResultDto> RunScan(
        TreasuryAlertScanRequestDto request);
}