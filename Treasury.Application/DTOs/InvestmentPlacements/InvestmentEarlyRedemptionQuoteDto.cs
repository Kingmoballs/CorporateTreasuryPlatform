namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentEarlyRedemptionQuoteDto
{
    public Guid InvestmentPlacementId { get; set; }

    public string InvestmentReference { get; set; } =
        string.Empty;

    public string InstitutionName { get; set; } =
        string.Empty;

    public string Currency { get; set; } = string.Empty;

    public DateTime StartDateUtc { get; set; }

    public DateTime OriginalMaturityDateUtc { get; set; }

    public DateTime ProposedRedemptionDateUtc
        { get; set; }

    public int InvestedDays { get; set; }

    public int RemainingDays { get; set; }

    public decimal PrincipalAmount { get; set; }

    public decimal AnnualInterestRate { get; set; }

    public int DayCountBasis { get; set; }

    public decimal PenaltyRatePercentage { get; set; }

    public decimal WithholdingTaxRatePercentage
        { get; set; }

    public decimal GrossAccruedInterestAmount
        { get; set; }

    public decimal PenaltyAmount { get; set; }

    public decimal InterestAfterPenaltyAmount
        { get; set; }

    public decimal WithholdingTaxAmount { get; set; }

    public decimal NetInterestAmount { get; set; }

    public decimal EstimatedRedemptionProceeds
        { get; set; }

    public decimal OriginalExpectedMaturityAmount
        { get; set; }

    public decimal ExpectedProceedsShortfall
        { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}