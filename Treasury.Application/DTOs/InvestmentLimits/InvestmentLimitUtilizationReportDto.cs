namespace Treasury.Application.DTOs.InvestmentLimits;

public class InvestmentLimitUtilizationReportDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public DateTime EffectiveAtUtc { get; set; }

    public int LimitCount { get; set; }

    public int WarningCount { get; set; }

    public int BreachedCount { get; set; }

    /*
     * These are committed placements that still have no
     * CounterpartyId. Assign them through the legacy
     * counterparty-assignment endpoint.
     */
    public int UnassignedPlacementCount { get; set; }

    public IReadOnlyList<
        InvestmentLimitUtilizationItemDto>
        Items { get; set; } =
            Array.Empty<
                InvestmentLimitUtilizationItemDto>();
}