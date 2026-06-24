namespace Treasury.Application.DTOs.Transfers;

public class TransferResponseDto
{
    public Guid FromAccountId { get; set; }

    public Guid ToAccountId { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; }
        = string.Empty;

    public DateTime Timestamp { get; set; }
}