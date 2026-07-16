namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentAccrualItemDto
{
    public Guid PlacementId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string InstitutionName { get; set; } =
        string.Empty;

    public string Currency { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal PrincipalAmount { get; set; }

    public decimal AnnualInterestRate { get; set; }

    public int DayCountBasis { get; set; }

    public DateTime StartDateUtc { get; set; }

    public DateTime MaturityDateUtc { get; set; }

    public DateTime AccrualThroughUtc { get; set; }

    public int ContractDays { get; set; }

    public int AccruedDays { get; set; }

    public int RemainingDays { get; set; }

    public bool IsOutstandingAsOf { get; set; }

    public bool IsRedeemedAsOf { get; set; }

    public decimal ExpectedInterestAmount { get; set; }

    public decimal AccruedInterestAmount { get; set; }

    public decimal CarryingAmount { get; set; }

    public decimal ActualInterestAmount { get; set; }

    public decimal WithholdingTaxAmount { get; set; }

    public decimal RealizedNetInterestAmount { get; set; }

    public decimal ActualRedemptionProceeds { get; set; }

    public decimal? InterestVarianceAmount { get; set; }

    public decimal? RealizedAnnualizedYieldPercentage
        { get; set; }

    public DateTime? RedeemedAtUtc { get; set; }
}