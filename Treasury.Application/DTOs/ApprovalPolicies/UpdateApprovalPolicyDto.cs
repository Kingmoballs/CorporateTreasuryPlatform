namespace Treasury.Application.DTOs.ApprovalPolicies;

public class UpdateApprovalPolicyDto
{
    public string OperationType { get; set; }
        = string.Empty;

    public string Currency { get; set; }
        = string.Empty;

    public decimal ThresholdAmount { get; set; }

    public int RequiredApprovalCount { get; set; }
        = 1;

    public int PendingRequestExpiryHours { get; set; }
        = 24;

    public bool IsActive { get; set; } = true;

}