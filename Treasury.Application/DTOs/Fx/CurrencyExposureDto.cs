namespace Treasury.Application.DTOs.Fx;

public class CurrencyExposureDto
{
    public string Currency { get; set; } = string.Empty;

    public int AccountCount { get; set; }

    public decimal TotalBalance { get; set; }

    public decimal TotalAvailableBalance { get; set; }

    public decimal TotalReservedBalance { get; set; }

    public decimal EffectiveRateToBaseCurrency { get; set; }

    public Guid? FxRateId { get; set; }

    public DateTime? FxRateDateUtc { get; set; }

    public bool UsedInverseRate { get; set; }

    public decimal TotalBalanceInBaseCurrency { get; set; }

    public decimal TotalAvailableBalanceInBaseCurrency { get; set; }

    public decimal TotalReservedBalanceInBaseCurrency { get; set; }

    public decimal PercentageOfTotalAvailableLiquidity { get; set; }
}