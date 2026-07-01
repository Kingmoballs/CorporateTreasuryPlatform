namespace Treasury.Domain.Entities;

public class TransferRequest
{
    public Guid Id { get; set; }

    public Guid FromAccountId { get; set; }

    public Guid ToAccountId { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = "Pending";

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;
}