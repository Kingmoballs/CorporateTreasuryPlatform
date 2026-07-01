namespace Treasury.Application.DTOs.Reporting;

public class LiquidityReportDto
{
    public DateTime ActivityFromUtc { get; set; }

    public DateTime ActivityToUtc { get; set; }

    public DateTime CashPositionAsOfUtc { get; set; }

    public IReadOnlyList<CurrencyLiquidityDto>
        Currencies { get; set; }
        = Array.Empty<CurrencyLiquidityDto>();
}