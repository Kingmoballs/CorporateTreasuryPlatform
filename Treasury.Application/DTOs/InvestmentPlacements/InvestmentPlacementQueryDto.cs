namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentPlacementQueryDto
{
    public string? Status { get; set; }

    public string? InvestmentType { get; set; }

    public string? InstitutionName { get; set; }

    public Guid? SourceAccountId { get; set; }

    public string? Currency { get; set; }

    public DateTime? MaturityFromUtc { get; set; }

    public DateTime? MaturityToUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}