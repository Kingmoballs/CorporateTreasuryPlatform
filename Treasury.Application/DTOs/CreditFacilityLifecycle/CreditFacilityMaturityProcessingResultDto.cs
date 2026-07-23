namespace Treasury.Application.DTOs.CreditFacilityLifecycle;

public class CreditFacilityMaturityProcessingResultDto
{
    public DateTime AsOfDateUtc { get; set; }

    public int FacilitiesSelected { get; set; }

    public int FacilitiesMatured { get; set; }

    public int OverdueAlertsCreated { get; set; }

    public List<CreditFacilityMaturityProcessingItemDto>
        Items { get; set; } = new();
}