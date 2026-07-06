namespace Treasury.Application.DTOs.BankStatements;

public class CreateBankStatementLineDto
{
    public int LineNumber { get; set; }

    public DateTime TransactionDateUtc { get; set; }

    public DateTime? ValueDateUtc { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? BankReference { get; set; }

    public string? CounterpartyName { get; set; }

    /*
     * Positive amount = money came in.
     * Negative amount = money went out.
     */
    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public decimal? BalanceAfterTransaction { get; set; }
}