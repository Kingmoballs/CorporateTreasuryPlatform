namespace Treasury.Application.DTOs.Fx;

public class CurrencyConversionResponseDto
{
    public decimal Amount { get; set; }

    public string FromCurrency { get; set; } = string.Empty;

    public string ToCurrency { get; set; } = string.Empty;

    public decimal ConvertedAmount { get; set; }

    public decimal EffectiveRate { get; set; }

    public Guid? FxRateId { get; set; }

    public DateTime? FxRateDateUtc { get; set; }

    public bool UsedInverseRate { get; set; }

    public DateTime AsOfUtc { get; set; }
}