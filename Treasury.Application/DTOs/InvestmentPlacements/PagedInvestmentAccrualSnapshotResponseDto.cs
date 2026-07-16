namespace Treasury.Application.DTOs.InvestmentPlacements;

public class PagedInvestmentAccrualSnapshotResponseDto
{
    public List<InvestmentAccrualSnapshotResponseDto>
        Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}