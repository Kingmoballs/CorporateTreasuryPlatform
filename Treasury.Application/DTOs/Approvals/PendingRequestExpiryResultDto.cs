namespace Treasury.Application.DTOs.Approvals;

public class PendingRequestExpiryResultDto
{
    public DateTime ProcessedAtUtc { get; set; }

    public int ExpiredTransferCount { get; set; }

    public int ExpiredPaymentCount { get; set; }

    public int ExpiredReversalCount { get; set; }

    public int TotalExpiredCount =>
        ExpiredTransferCount +
        ExpiredPaymentCount +
        ExpiredReversalCount;
}