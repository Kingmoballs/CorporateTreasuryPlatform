namespace Treasury.Application.DTOs.Reporting;

public class BalanceAggregationDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public IReadOnlyList<CurrencyBalanceSummaryDto>
        Currencies { get; set; }
        = Array.Empty<CurrencyBalanceSummaryDto>();
}
