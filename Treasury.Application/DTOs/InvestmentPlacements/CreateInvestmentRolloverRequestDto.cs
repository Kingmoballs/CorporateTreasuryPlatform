namespace Treasury.Application.DTOs.InvestmentPlacements;

public class CreateInvestmentRolloverRequestDto
    : InvestmentRolloverQuoteRequestDto
{
    /*
     * Required only when PrincipalOnly produces a
     * positive cash payout.
     */
    public Guid? CashPayoutAccountId { get; set; }

    public string IdempotencyKey { get; set; } =
        string.Empty;

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }
}