namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentAccrualCurrencySummaryDto
{
    public string Currency { get; set; } = string.Empty;

    public int PlacementCount { get; set; }

    public int OutstandingPlacementCount { get; set; }

    public int RedeemedPlacementCount { get; set; }

    public decimal OutstandingPrincipal { get; set; }

    public decimal AccruedInterestAmount { get; set; }

    public decimal CarryingAmount { get; set; }

    public decimal OutstandingExpectedInterestAmount
        { get; set; }

    public decimal RealizedGrossInterestAmount
        { get; set; }

    public decimal WithholdingTaxAmount { get; set; }

    public decimal RealizedNetInterestAmount
        { get; set; }

    public decimal ActualRedemptionProceeds
        { get; set; }

    public decimal InterestVarianceAmount { get; set; }

    public decimal WeightedAverageContractRate
        { get; set; }
}