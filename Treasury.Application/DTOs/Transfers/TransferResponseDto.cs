namespace Treasury.Application.DTOs.Transfers;

public class TransferResponseDto
{
    public Guid FromAccountId { get; set; }

    public Guid ToAccountId { get; set; }

    public Guid? TransactionId { get; set; }

    public string? TransactionReference { get; set; }

    public string Status { get; set; }
        = string.Empty;

    public decimal Amount { get; set; }

    public string Description { get; set; }
        = string.Empty;
    
    public int ApprovalCount { get; set; }

    public int RequiredApprovalCount { get; set; }

    public DateTime Timestamp { get; set; }
}