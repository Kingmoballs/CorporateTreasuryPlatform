namespace Treasury.Application.DTOs.CreditFacilityLifecycle;

public class CreditFacilityMaturityProcessingItemDto
{
    public Guid CreditFacilityId { get; set; }

    public string FacilityReference { get; set; } =
        string.Empty;

    public string Currency { get; set; } =
        string.Empty;

    public DateTime MaturityDateUtc { get; set; }

    public decimal OutstandingPrincipalAmount
        { get; set; }

    public decimal AccruedInterestAmount
        { get; set; }

    public decimal TotalOutstandingAmount
        { get; set; }

    public bool OverdueAlertCreated { get; set; }
}