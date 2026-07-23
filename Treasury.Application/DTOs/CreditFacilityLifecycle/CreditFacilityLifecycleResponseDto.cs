namespace Treasury.Application.DTOs.CreditFacilityLifecycle;

public class CreditFacilityLifecycleResponseDto
{
    public Guid Id { get; set; }

    public string Reference { get; set; } =
        string.Empty;

    public string FacilityName { get; set; } =
        string.Empty;

    public string FacilityType { get; set; } =
        string.Empty;

    public string LenderName { get; set; } =
        string.Empty;

    public string Currency { get; set; } =
        string.Empty;

    public string Status { get; set; } =
        string.Empty;

    public decimal ApprovedLimitAmount { get; set; }

    public decimal OutstandingPrincipalAmount
        { get; set; }

    public decimal AccruedInterestAmount
        { get; set; }

    public decimal TotalOutstandingAmount
        { get; set; }

    public decimal AvailableAmount { get; set; }

    public DateTime MaturityDateUtc { get; set; }

    public Guid? SuspendedByUserId { get; set; }

    public DateTime? SuspendedAtUtc { get; set; }

    public string? SuspensionReason { get; set; }

    public DateTime? MaturedAtUtc { get; set; }

    public Guid? ClosedByUserId { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public string? ClosureReason { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}