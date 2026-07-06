namespace Treasury.Application.DTOs.ApprovalPolicies;

public class ApprovalPolicyDto
{
    public Guid Id { get; set; }

    public string OperationType { get; set; }
        = string.Empty;

    public string Currency { get; set; }
        = string.Empty;

    public decimal ThresholdAmount { get; set; }

    public bool IsActive { get; set; }

    public int RequiredApprovalCount { get; set; }

    public int PendingRequestExpiryHours { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }    
}