namespace Treasury.Application.DTOs.CreditFacilityAccruals;

public class GenerateCreditFacilityAccrualsDto
{
    public DateTime? AsOfDateUtc { get; set; }

    public Guid? CreditFacilityId { get; set; }

    public int MaxFacilities { get; set; } = 100;

    public int MaxAccrualDaysPerFacility
        { get; set; } = 366;
}