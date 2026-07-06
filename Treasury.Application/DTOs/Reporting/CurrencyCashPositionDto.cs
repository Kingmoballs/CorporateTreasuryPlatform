namespace Treasury.Application.DTOs.Reporting;

public class CurrencyCashPositionDto
{
    public string Currency { get; set; }
        = string.Empty;

    public decimal TotalCash { get; set; }

    public decimal AvailableLiquidity { get; set; }

    public decimal CommittedCash { get; set; }

    public decimal InvestmentBalance { get; set; }

    public decimal OtherBalance { get; set; }

    public decimal PendingInternalTransferAmount { get; set; }
    
    public decimal AvailableLiquidityRatio { get; set; }

    public decimal ReservedCash { get; set; }

    public IReadOnlyList<CashPositionAccountDto>
        Accounts { get; set; }
        = Array.Empty<CashPositionAccountDto>();
}