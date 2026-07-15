namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentPortfolioBucketDto
{
    public string Currency { get; set; } = string.Empty;

    public string InstitutionName { get; set; } =
        string.Empty;

    public int PlacementCount { get; set; }

    public int ActiveCount { get; set; }

    public int MaturedCount { get; set; }

    public int RedeemedCount { get; set; }

    public int OverdueUnredeemedCount { get; set; }

    public decimal OutstandingPrincipal { get; set; }

    public decimal OutstandingExpectedInterest
        { get; set; }

    public decimal OutstandingExpectedMaturityAmount
        { get; set; }

    public decimal ActualRedeemedProceeds { get; set; }

    public decimal WeightedAverageInterestRate
        { get; set; }

    public DateTime? NextMaturityDateUtc { get; set; }
}