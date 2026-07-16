namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentAccrualSnapshotQueryDto
{
    public DateTime? SnapshotDateFromUtc { get; set; }

    public DateTime? SnapshotDateToUtc { get; set; }

    public string? Currency { get; set; }

    public string? InstitutionName { get; set; }

    public Guid? InvestmentPlacementId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}
