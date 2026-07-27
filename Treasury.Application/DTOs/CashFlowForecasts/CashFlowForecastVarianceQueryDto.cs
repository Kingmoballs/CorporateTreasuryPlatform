namespace Treasury.Application.DTOs.CashFlowForecasts;

public class CashFlowForecastVarianceQueryDto
{
    public Guid? AccountId { get; set; }

    public string? Currency { get; set; }

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }
}
