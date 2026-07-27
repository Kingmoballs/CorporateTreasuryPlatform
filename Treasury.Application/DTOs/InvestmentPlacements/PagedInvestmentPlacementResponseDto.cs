namespace Treasury.Application.DTOs.InvestmentPlacements;

public class PagedInvestmentPlacementResponseDto
{
    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public List<InvestmentPlacementResponseDto> Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
