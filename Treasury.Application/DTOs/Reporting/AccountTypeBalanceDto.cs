namespace Treasury.Application.DTOs.Reporting;

public class AccountTypeBalanceDto
{
    public string AccountType { get; set; }
        = string.Empty;

    public int AccountCount { get; set; }

    public decimal TotalBalance { get; set; }
}