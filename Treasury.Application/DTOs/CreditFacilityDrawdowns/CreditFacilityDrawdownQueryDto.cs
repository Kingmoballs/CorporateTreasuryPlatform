namespace Treasury.Application.DTOs.CreditFacilityDrawdowns;

public class CreditFacilityDrawdownQueryDto
{
    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}