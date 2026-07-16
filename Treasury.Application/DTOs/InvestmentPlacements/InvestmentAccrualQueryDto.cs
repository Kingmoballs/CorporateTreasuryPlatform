namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentAccrualQueryDto
{
    public DateTime? AsOfUtc { get; set; }

    public string? Currency { get; set; }

    public string? InstitutionName { get; set; }

    public bool IncludeRedeemed { get; set; }
}