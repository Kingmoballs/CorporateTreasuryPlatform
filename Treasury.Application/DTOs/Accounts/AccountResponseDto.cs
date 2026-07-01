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

    public decimal Balance { get; set; }

    public string Currency { get; set; }
        = string.Empty;
    
    public string?
        OpeningBalanceTransactionReference { get; set; }

}