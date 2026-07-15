namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentMaturityProcessingResultDto
{
    public DateTime ProcessedAtUtc { get; set; }

    public int MaturedCount { get; set; }

    public List<Guid> PlacementIds { get; set; } =
        new();
}