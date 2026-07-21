namespace Treasury.Application.DTOs.InvestmentLimits;

public class CreateInvestmentLimitDto
{
    public Guid CounterpartyId { get; set; }

    public string Currency { get; set; } = string.Empty;

    /*
     * Use "All" for an overall counterparty limit or
     * "FixedDeposit" for a product-specific limit.
     */
    public string InvestmentType { get; set; } = "All";

    public decimal MaximumExposureAmount { get; set; }

    public decimal WarningThresholdPercentage
        { get; set; } = 80m;

    public DateTime EffectiveFromUtc { get; set; }

    public DateTime? EffectiveToUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }
}