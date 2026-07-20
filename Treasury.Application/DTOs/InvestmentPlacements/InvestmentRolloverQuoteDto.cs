namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentRolloverQuoteDto
{
    public Guid OriginalInvestmentPlacementId
        { get; set; }

    public string OriginalInvestmentReference
        { get; set; } = string.Empty;

    public string OriginalInvestmentStatus
        { get; set; } = string.Empty;

    public string OriginalInvestmentType
        { get; set; } = string.Empty;

    public string OriginalInstitutionName
        { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public DateTime OriginalMaturityDateUtc
        { get; set; }

    public decimal OriginalPrincipalAmount
        { get; set; }

    public decimal GrossInterestAmount { get; set; }

    public decimal GrossMaturityAmount { get; set; }

    public decimal WithholdingTaxRatePercentage
        { get; set; }

    public decimal WithholdingTaxAmount { get; set; }

    public decimal NetInterestAmount { get; set; }

    public decimal NetMaturityProceeds { get; set; }

    public string RolloverOption { get; set; } =
        string.Empty;

    /*
     * Amount that will become the principal of the
     * replacement investment.
     */
    public decimal RolloverPrincipalAmount
        { get; set; }

    /*
     * For PrincipalOnly, this is the net interest that
     * will eventually be paid to a destination account.
     */
    public decimal CashPayoutAmount { get; set; }

    public string NewInvestmentType { get; set; } =
        string.Empty;

    public string NewInstitutionName { get; set; } =
        string.Empty;

    public decimal NewAnnualInterestRate { get; set; }

    public int NewDayCountBasis { get; set; }

    public DateTime NewStartDateUtc { get; set; }

    public DateTime NewMaturityDateUtc { get; set; }

    public int NewTenorDays { get; set; }

    public decimal NewExpectedInterestAmount
        { get; set; }

    public decimal NewExpectedMaturityAmount
        { get; set; }

    /*
     * This is true only when the original maturity date
     * has been reached. Approval will still be required
     * in the next implementation stage.
     */
    public bool CanExecuteNow { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}