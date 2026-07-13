namespace Treasury.Application.DTOs.TreasuryAlerts;

public class PagedTreasuryAlertResponseDto
{
    public List<TreasuryAlertResponseDto> Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}