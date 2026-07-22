namespace Treasury.Application.DTOs.CreditFacilities;

public class CreditFacilityQueryDto
{
    public string? Status { get; set; }

    public string? FacilityType { get; set; }

    public string? FacilityName { get; set; }

    public Guid? LenderCounterpartyId { get; set; }

    public Guid? SettlementAccountId { get; set; }

    public string? Currency { get; set; }

    public DateTime? MaturityFromUtc { get; set; }

    public DateTime? MaturityToUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}