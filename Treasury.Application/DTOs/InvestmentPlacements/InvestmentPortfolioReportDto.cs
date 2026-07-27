namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentPortfolioReportDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public Guid? SourceAccountId { get; set; }

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public string? CurrencyFilter { get; set; }

    public string? InstitutionFilter { get; set; }

    public DateTime? MaturityFromUtc { get; set; }

    public DateTime? MaturityToUtc { get; set; }

    public bool IncludesRedeemed { get; set; }

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

    public decimal RedeemedPrincipal { get; set; }

    public decimal ActualInterestEarned { get; set; }

    public decimal WithholdingTaxAmount { get; set; }

    public decimal ActualRedeemedProceeds { get; set; }

    public decimal WeightedAverageInterestRate
        { get; set; }

    public DateTime? NextMaturityDateUtc { get; set; }

    public List<InvestmentPortfolioBucketDto>
        Buckets { get; set; } = new();
}
