namespace Treasury.Application.DTOs.TreasuryAlerts;

public class PagedTreasuryAlertResponseDto
{
    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public List<TreasuryAlertResponseDto> Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
