namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentMaturityScheduleDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public int PlacementCount { get; set; }

    public int OverdueCount { get; set; }

    public decimal TotalPrincipalAmount { get; set; }

    public decimal TotalExpectedMaturityAmount
        { get; set; }

    public List<InvestmentMaturityScheduleItemDto>
        Items { get; set; } = new();
}