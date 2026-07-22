namespace Treasury.Application.DTOs.CreditFacilityDrawdowns;

public class PagedCreditFacilityDrawdownResponseDto
{
    public List<CreditFacilityDrawdownResponseDto>
        Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}