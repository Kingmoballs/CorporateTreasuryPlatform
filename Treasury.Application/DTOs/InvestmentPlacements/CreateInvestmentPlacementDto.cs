namespace Treasury.Application.DTOs.InvestmentPlacements;

public class CreateInvestmentPlacementDto
{
    public Guid SourceAccountId { get; set; }

    public string InvestmentType { get; set; } = "FixedDeposit";

    public Guid CounterpartyId { get; set; }

    public decimal PrincipalAmount { get; set; }

    public decimal AnnualInterestRate { get; set; }

    public DateTime StartDateUtc { get; set; }

    public DateTime MaturityDateUtc { get; set; }

    public int DayCountBasis { get; set; } = 365;

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }
}