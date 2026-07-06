namespace Treasury.Domain.Entities;

public class ApprovalPolicy
{
    public Guid Id { get; set; }

    public string OperationType { get; set; }
        = string.Empty;

    public string Currency { get; set; }
        = string.Empty;

    public decimal ThresholdAmount { get; set; }

    public int RequiredApprovalCount { get; set; }
        = 1;

    public bool IsActive { get; set; } = true;

    public Guid? UpdatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public Guid ConcurrencyToken { get; set; }
        = Guid.NewGuid();
}