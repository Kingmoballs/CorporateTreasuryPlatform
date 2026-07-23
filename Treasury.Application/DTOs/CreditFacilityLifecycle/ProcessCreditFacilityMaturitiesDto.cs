namespace Treasury.Application.DTOs.CreditFacilityLifecycle;

public class ProcessCreditFacilityMaturitiesDto
{
    /*
     * Defaults to today's UTC date.
     */
    public DateTime? AsOfDateUtc { get; set; }

    public int MaxRows { get; set; } = 100;
}