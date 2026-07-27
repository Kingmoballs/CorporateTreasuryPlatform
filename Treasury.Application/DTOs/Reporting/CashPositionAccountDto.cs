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

    public Guid? LegalEntityId { get; set; }

    public string? LegalEntityCode { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public string? BusinessUnitCode { get; set; }

    public decimal Balance { get; set; }

    public decimal ReservedBalance { get; set; }

    public decimal AvailableBalance { get; set; }
}
