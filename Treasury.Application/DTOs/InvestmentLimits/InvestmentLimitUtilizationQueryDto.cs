namespace Treasury.Application.DTOs.InvestmentLimits;

public class InvestmentLimitUtilizationQueryDto
{
    public Guid? CounterpartyId { get; set; }

    public string? Currency { get; set; }

    public DateTime? AsOfUtc { get; set; }
}