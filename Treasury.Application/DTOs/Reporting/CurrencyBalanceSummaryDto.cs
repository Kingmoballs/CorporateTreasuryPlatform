namespace Treasury.Application.DTOs.Reporting;

public class CurrencyBalanceSummaryDto
{
    public string Currency { get; set; }
        = string.Empty;

    public int AccountCount { get; set; }

    public decimal TotalBalance { get; set; }

    public IReadOnlyList<AccountTypeBalanceDto>
        ByAccountType { get; set; }
        = Array.Empty<AccountTypeBalanceDto>();
}