namespace Treasury.Application.DTOs.Counterparties;

public class CounterpartyQueryDto
{
    public string? Search { get; set; }

    public string? CounterpartyType { get; set; }

    public bool? IsActive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}