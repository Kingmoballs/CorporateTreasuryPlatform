namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentAccrualReportDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public DateTime AsOfUtc { get; set; }

    public string? CurrencyFilter { get; set; }

    public string? InstitutionFilter { get; set; }

    public bool IncludesRedeemed { get; set; }

    public int PlacementCount { get; set; }

    public int OutstandingPlacementCount { get; set; }

    public int RedeemedPlacementCount { get; set; }

    /*
     * Monetary totals are kept per currency so that
     * NGN, USD and other currencies are not incorrectly
     * added together.
     */
    public List<InvestmentAccrualCurrencySummaryDto>
        Currencies { get; set; } = new();

    public List<InvestmentAccrualItemDto>
        Items { get; set; } = new();
}