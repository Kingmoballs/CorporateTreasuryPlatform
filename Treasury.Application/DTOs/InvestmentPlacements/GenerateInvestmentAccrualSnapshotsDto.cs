namespace Treasury.Application.DTOs.InvestmentPlacements;

public class GenerateInvestmentAccrualSnapshotsDto
{
    /*
     * Defaults to today's UTC date when omitted.
     */
    public DateTime? SnapshotDateUtc { get; set; }

    public string? Currency { get; set; }

    public string? InstitutionName { get; set; }

    public bool IncludeRedeemed { get; set; }
}