namespace Treasury.Application.DTOs.Fx;

public class CurrencyExposureReportDto
{
    public string BaseCurrency { get; set; } = string.Empty;

    public DateTime AsOfUtc { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public decimal TotalAvailableLiquidityInBaseCurrency { get; set; }

    public List<CurrencyExposureDto> Exposures { get; set; }
        = new();
}