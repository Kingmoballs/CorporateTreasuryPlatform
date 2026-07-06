namespace Treasury.Application.DTOs.ApprovalPolicies;

public class ApprovalRequirementsDto
{
    public decimal ThresholdAmount { get; set; }

    public int RequiredApprovalCount { get; set; }

    public int PendingRequestExpiryHours { get; set; }
        = 24;
}