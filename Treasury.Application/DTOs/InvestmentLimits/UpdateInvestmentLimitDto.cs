namespace Treasury.Application.DTOs.InvestmentLimits;

public class UpdateInvestmentLimitDto
{
    public decimal MaximumExposureAmount { get; set; }

    public decimal WarningThresholdPercentage
        { get; set; } = 80m;

    public DateTime EffectiveFromUtc { get; set; }

    public DateTime? EffectiveToUtc { get; set; }

    public string? Notes { get; set; }
}