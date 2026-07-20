namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentRolloverQuoteRequestDto
{
    /*
     * PrincipalOnly:
     * Only the original principal is reinvested.
     *
     * PrincipalAndNetInterest:
     * Principal and interest remaining after withholding
     * tax are reinvested.
     */
    public string RolloverOption { get; set; } =
        "PrincipalAndNetInterest";

    /*
     * When omitted, the original placement's expected
     * interest is used.
     *
     * At maturity, the bank-confirmed gross interest can
     * be supplied here for a more accurate quote.
     */
    public decimal? GrossInterestAmount { get; set; }

    public decimal WithholdingTaxRatePercentage
        { get; set; }

    /*
     * When omitted, the original investment type and
     * institution are retained.
     */
    public string? NewInvestmentType { get; set; }

    public string? NewInstitutionName { get; set; }

    public decimal NewAnnualInterestRate { get; set; }

    public int NewDayCountBasis { get; set; } = 365;

    /*
     * When omitted, the later of the original maturity
     * date or today's UTC date is used.
     */
    public DateTime? NewStartDateUtc { get; set; }

    public DateTime NewMaturityDateUtc { get; set; }
}