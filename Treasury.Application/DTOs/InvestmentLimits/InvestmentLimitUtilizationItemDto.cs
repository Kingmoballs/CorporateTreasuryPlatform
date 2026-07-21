namespace Treasury.Application.DTOs.InvestmentLimits;

public class InvestmentLimitUtilizationItemDto
{
    public Guid InvestmentLimitId { get; set; }

    public Guid CounterpartyId { get; set; }

    public string CounterpartyCode { get; set; } =
        string.Empty;

    public string CounterpartyName { get; set; } =
        string.Empty;

    public string Currency { get; set; } =
        string.Empty;

    public string InvestmentType { get; set; } =
        string.Empty;

    public decimal MaximumExposureAmount { get; set; }

    public decimal WarningThresholdPercentage
        { get; set; }

    public decimal WarningThresholdAmount { get; set; }

    public int PlacementCount { get; set; }

    public decimal CurrentExposureAmount { get; set; }

    public decimal AvailableLimitAmount { get; set; }

    public decimal BreachAmount { get; set; }

    public decimal UtilizationPercentage { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime EffectiveFromUtc { get; set; }

    public DateTime? EffectiveToUtc { get; set; }
}