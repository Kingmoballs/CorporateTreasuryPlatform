namespace Treasury.Application.DTOs.InvestmentPlacements;

public class CreateInvestmentEarlyRedemptionRequestDto
{
    public Guid DestinationAccountId { get; set; }

    public DateTime? ProposedRedemptionDateUtc
        { get; set; }

    public decimal PenaltyRatePercentage { get; set; }

    public decimal WithholdingTaxRatePercentage
        { get; set; }

    public string IdempotencyKey { get; set; } =
        string.Empty;

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }
}