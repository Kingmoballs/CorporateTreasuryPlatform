namespace Treasury.Application.DTOs.CreditFacilityAccruals;

public class CreditFacilityAccrualGenerationResultDto
{
    public DateTime AsOfDateUtc { get; set; }

    public int FacilitiesSelected { get; set; }

    public int FacilitiesProcessed { get; set; }

    public int FacilitiesSkipped { get; set; }

    public int SnapshotsCreated { get; set; }

    public decimal TotalInterestAccrued { get; set; }

    public List<CreditFacilityAccrualGenerationItemDto>
        Items { get; set; } = new();
}