namespace Treasury.Application.DTOs.CreditFacilities;

public class UpdateCreditFacilityDto
{
    public string FacilityName { get; set; } =
        string.Empty;

    public string FacilityType { get; set; } =
        "RevolvingCredit";

    public Guid LenderCounterpartyId { get; set; }

    public Guid SettlementAccountId { get; set; }

    public decimal ApprovedLimitAmount { get; set; }

    public decimal AnnualInterestRate { get; set; }

    public decimal CommitmentFeeRatePercentage
        { get; set; }

    public decimal ArrangementFeeAmount { get; set; }

    public int DayCountBasis { get; set; } = 365;

    public string InterestPaymentFrequency
        { get; set; } = "Monthly";

    public DateTime StartDateUtc { get; set; }

    public DateTime MaturityDateUtc { get; set; }

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }
}