namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentMaturityScheduleItemDto
{
    public Guid PlacementId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string InstitutionName { get; set; } =
        string.Empty;

    public string Currency { get; set; } = string.Empty;

    public decimal PrincipalAmount { get; set; }

    public decimal AnnualInterestRate { get; set; }

    public decimal ExpectedInterestAmount { get; set; }

    public decimal ExpectedMaturityAmount { get; set; }

    public DateTime StartDateUtc { get; set; }

    public DateTime MaturityDateUtc { get; set; }

    public int DaysToMaturity { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsOverdue { get; set; }

    public decimal ActualMaturityAmount { get; set; }

    public DateTime? RedeemedAtUtc { get; set; }
}