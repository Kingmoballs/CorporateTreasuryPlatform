namespace Treasury.Application.DTOs.Reporting;

public class CurrencyLiquidityDto
{
    public string Currency { get; set; }
        = string.Empty;

    public decimal CurrentTotalCash { get; set; }

    public decimal AvailableLiquidity { get; set; }

    public decimal CommittedCash { get; set; }

    public decimal InvestmentBalance { get; set; }

    public decimal OtherBalance { get; set; }

    public decimal AvailableLiquidityRatio
    {
        get;
        set;
    }

    public int CompletedInternalTransferCount
    {
        get;
        set;
    }

    public decimal CompletedInternalTransferVolume
    {
        get;
        set;
    }

    public int PendingInternalTransferCount
    {
        get;
        set;
    }

    public decimal PendingInternalTransferAmount
    {
        get;
        set;
    }

    public int ExternalReceiptCount { get; set; }

    public decimal ExternalReceiptAmount { get; set; }

    public int ExternalPaymentCount { get; set; }

    public decimal ExternalPaymentAmount { get; set; }

    public decimal NetExternalCashFlow { get; set; }

    public int ReversedReceiptCount { get; set; }

    public decimal ReversedReceiptAmount
    {
        get;
        set;
    }

    public int ReversedPaymentCount { get; set; }

    public decimal ReversedPaymentAmount
    {
        get;
        set;
    }

    public decimal ReservedCash { get; set; }
}