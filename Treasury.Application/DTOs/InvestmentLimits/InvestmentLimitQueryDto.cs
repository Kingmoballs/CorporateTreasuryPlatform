namespace Treasury.Application.DTOs.InvestmentLimits;

public class InvestmentLimitQueryDto
{
    public Guid? CounterpartyId { get; set; }

    public string? Currency { get; set; }

    public string? InvestmentType { get; set; }

    public bool? IsActive { get; set; }

    /*
     * When supplied, only limits applicable at this
     * point in time are returned.
     */
    public DateTime? AsOfUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}