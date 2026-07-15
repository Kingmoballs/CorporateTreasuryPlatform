namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentPortfolioQueryDto
{
    public string? Currency { get; set; }

    public string? InstitutionName { get; set; }

    public DateTime? MaturityFromUtc { get; set; }

    public DateTime? MaturityToUtc { get; set; }

    public bool IncludeRedeemed { get; set; }
}