namespace Treasury.Application.DTOs.Reporting;

public class CashPositionAccountDto
{
    public Guid AccountId { get; set; }

    public string Name { get; set; }
        = string.Empty;

    public string AccountNumber { get; set; }
        = string.Empty;

    public string AccountType { get; set; }
        = string.Empty;

    public decimal Balance { get; set; }

    public decimal ReservedBalance { get; set; }

    public decimal AvailableBalance { get; set; }
}