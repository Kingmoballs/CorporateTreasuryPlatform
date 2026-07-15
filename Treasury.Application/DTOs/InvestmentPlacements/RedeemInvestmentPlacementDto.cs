namespace Treasury.Application.DTOs.InvestmentPlacements;

public class RedeemInvestmentPlacementDto
{
    public Guid DestinationAccountId { get; set; }

    public decimal ActualInterestAmount { get; set; }

    public decimal WithholdingTaxAmount { get; set; }

    public string IdempotencyKey { get; set; } =
        string.Empty;

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }
}