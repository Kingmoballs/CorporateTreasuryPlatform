namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentEarlyRedemptionQuoteRequestDto
{
    /*
     * Defaults to today's UTC date when omitted.
     * A future date may be supplied for planning.
     */
    public DateTime? ProposedRedemptionDateUtc
        { get; set; }

    /*
     * Percentage of accrued interest forfeited as
     * an early-redemption penalty.
     */
    public decimal PenaltyRatePercentage { get; set; }

    /*
     * This is intentionally not given a legal default.
     * The appropriate rate should be supplied according
     * to the investment and applicable tax rules.
     */
    public decimal WithholdingTaxRatePercentage
        { get; set; }
}