namespace Treasury.Application.DTOs.CashMovements;

public class CreateCashReceiptDto
{
    public Guid AccountId { get; set; }

    public decimal Amount { get; set; }

    public string CounterpartyName { get; set; }
        = string.Empty;

    public string Category { get; set; }
        = string.Empty;

    public string? ExternalReference { get; set; }

    public string IdempotencyKey { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;
}