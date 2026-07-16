namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentAccrualSnapshotGenerationResultDto
{
    public DateTime SnapshotDateUtc { get; set; }

    public int EligiblePlacementCount { get; set; }

    public int CreatedSnapshotCount { get; set; }

    public int SkippedDuplicateCount { get; set; }

    public List<InvestmentAccrualSnapshotResponseDto>
        CreatedSnapshots { get; set; } = new();
}