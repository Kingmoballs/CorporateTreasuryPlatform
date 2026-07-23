namespace Treasury.Application.DTOs.CreditFacilityAccruals;

public class CreditFacilityAccrualSnapshotResponseDto
{
    public Guid Id { get; set; }

    public Guid CreditFacilityId { get; set; }

    public DateTime SnapshotDateUtc { get; set; }

    public string FacilityReference { get; set; } =
        string.Empty;

    public string FacilityName { get; set; } =
        string.Empty;

    public string LenderName { get; set; } =
        string.Empty;

    public string Currency { get; set; } =
        string.Empty;

    public string FacilityStatus { get; set; } =
        string.Empty;

    public decimal OutstandingPrincipalAmount
        { get; set; }

    public decimal AnnualInterestRate { get; set; }

    public int DayCountBasis { get; set; }

    public int AccruedDays { get; set; }

    public decimal AccruedInterestBefore
        { get; set; }

    public decimal AccruedInterestAmount
        { get; set; }

    public decimal AccruedInterestAfter
        { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}