using Treasury.Application.DTOs.Reporting;

namespace Treasury.Application.Interfaces;

public interface ITreasuryReportingService
{
    Task<BalanceAggregationDto>
        GetBalanceAggregation();
}