namespace Treasury.Application.DTOs.InvestmentLimits;

public class PagedInvestmentLimitResponseDto
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public IReadOnlyList<InvestmentLimitResponseDto>
        Items { get; set; } =
            Array.Empty<InvestmentLimitResponseDto>();
}