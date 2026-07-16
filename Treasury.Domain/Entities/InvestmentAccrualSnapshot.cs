namespace Treasury.Domain.Entities;

public class InvestmentAccrualSnapshot
{
    public Guid Id { get; set; }

    public Guid InvestmentPlacementId { get; set; }

    public InvestmentPlacement InvestmentPlacement
        { get; set; } = null!;

    public DateTime SnapshotDateUtc { get; set; }

    public string InvestmentReference { get; set; } =
        string.Empty;

    public string InstitutionName { get; set; } =
        string.Empty;

    public string Currency { get; set; } = string.Empty;

    public string PlacementStatus { get; set; } =
        string.Empty;

    public decimal PrincipalAmount { get; set; }

    public decimal AnnualInterestRate { get; set; }

    public int DayCountBasis { get; set; }

    public int AccruedDays { get; set; }

    public decimal ExpectedInterestAmount { get; set; }

    public decimal AccruedInterestAmount { get; set; }

    public decimal CarryingAmount { get; set; }

    public bool IsOutstandingAsOf { get; set; }

    public bool IsRedeemedAsOf { get; set; }

    public decimal ActualInterestAmount { get; set; }

    public decimal WithholdingTaxAmount { get; set; }

    public decimal RealizedNetInterestAmount { get; set; }

    public decimal ActualRedemptionProceeds { get; set; }

    public decimal? InterestVarianceAmount { get; set; }

    public decimal? RealizedAnnualizedYieldPercentage
        { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}