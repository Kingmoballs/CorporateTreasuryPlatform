namespace Treasury.Application.DTOs.Accounts;

public class AccountResponseDto
{
    public Guid Id { get; set; }

    public string Name { get; set; }
        = string.Empty;

    public string AccountNumber { get; set; }
        = string.Empty;

    public string AccountType { get; set; }
        = string.Empty;

    public Guid? LegalEntityId { get; set; }

    public string? LegalEntityCode { get; set; }

    public string? LegalEntityName { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public string? BusinessUnitCode { get; set; }

    public string? BusinessUnitName { get; set; }

    public decimal Balance { get; set; }

    public string Currency { get; set; }
        = string.Empty;
    
    public string?
        OpeningBalanceTransactionReference { get; set; }
    
    public decimal ReservedBalance { get; set; }

    public decimal AvailableBalance { get; set; }

}
