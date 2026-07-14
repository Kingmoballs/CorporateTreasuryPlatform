namespace Treasury.Application.DTOs.InvestmentPlacements;

public class PagedInvestmentPlacementResponseDto
{
    public List<InvestmentPlacementResponseDto> Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}