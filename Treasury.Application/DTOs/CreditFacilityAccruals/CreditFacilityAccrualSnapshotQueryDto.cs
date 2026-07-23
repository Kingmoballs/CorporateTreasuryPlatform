namespace Treasury.Application.DTOs.CreditFacilityAccruals;

public class CreditFacilityAccrualSnapshotQueryDto
{
    public Guid? CreditFacilityId { get; set; }

    public string? Currency { get; set; }

    public DateTime? SnapshotDateFromUtc { get; set; }

    public DateTime? SnapshotDateToUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}