namespace Treasury.Application.DTOs.CreditFacilities;

public class PagedCreditFacilityResponseDto
{
    public List<CreditFacilityResponseDto> Items
        { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}