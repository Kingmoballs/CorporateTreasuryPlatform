namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentPortfolioQueryDto
{
    public Guid? SourceAccountId { get; set; }

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public string? Currency { get; set; }

    public string? InstitutionName { get; set; }

    public Guid? CounterpartyId { get; set; }

    public DateTime? MaturityFromUtc { get; set; }

    public DateTime? MaturityToUtc { get; set; }

    public bool IncludeRedeemed { get; set; }
}
