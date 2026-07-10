namespace Treasury.Application.DTOs.Fx;

public class ConsolidatedCashPositionAccountDto
{
    public Guid AccountId { get; set; }

    public string AccountName { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public string? AccountType { get; set; }

    public string Currency { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public decimal AvailableBalance { get; set; }

    public decimal ReservedBalance { get; set; }

    public decimal EffectiveRate { get; set; }

    public Guid? FxRateId { get; set; }

    public DateTime? FxRateDateUtc { get; set; }

    public bool UsedInverseRate { get; set; }

    public decimal ConvertedBalance { get; set; }

    public decimal ConvertedAvailableBalance { get; set; }

    public decimal ConvertedReservedBalance { get; set; }
}