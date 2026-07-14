namespace Treasury.Application.DTOs.Transactions;

public class CurrencyTreasuryActivitySummaryDto
{
    public string Currency { get; set; } = string.Empty;

    public int TransactionCount { get; set; }

    public int CashReceiptCount { get; set; }

    public decimal CashReceiptAmount { get; set; }

    public int CashPaymentCount { get; set; }

    public decimal CashPaymentAmount { get; set; }

    public int ReversalCount { get; set; }

    public decimal ReversalAmount { get; set; }

    public int InternalTransferCount { get; set; }

    public decimal InternalTransferVolume { get; set; }

    public int OpeningBalanceCount { get; set; }

    public decimal OpeningBalanceAmount { get; set; }

    public decimal TotalInflowAmount { get; set; }

    public decimal TotalOutflowAmount { get; set; }

    public decimal NetCashMovement { get; set; }
}