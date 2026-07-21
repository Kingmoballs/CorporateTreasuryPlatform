namespace Treasury.Application.DTOs.Counterparties;

public class PagedCounterpartyResponseDto
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public IReadOnlyList<CounterpartyResponseDto>
        Items { get; set; } =
            Array.Empty<CounterpartyResponseDto>();
}