namespace Treasury.Application.DTOs.CreditFacilityAccruals;

public class CreditFacilityAccrualGenerationItemDto
{
    public Guid CreditFacilityId { get; set; }

    public string FacilityReference { get; set; } =
        string.Empty;

    public string Currency { get; set; } =
        string.Empty;

    public DateTime? FirstSnapshotDateUtc { get; set; }

    public DateTime? LastSnapshotDateUtc { get; set; }

    public int SnapshotsCreated { get; set; }

    public decimal AccruedInterestBefore { get; set; }

    public decimal InterestAccrued { get; set; }

    public decimal AccruedInterestAfter { get; set; }
}