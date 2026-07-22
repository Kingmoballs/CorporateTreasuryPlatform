namespace Treasury.Application.DTOs.CreditFacilityRepayments;

public class PagedCreditFacilityRepaymentResponseDto
{
    public List<CreditFacilityRepaymentResponseDto>
        Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}