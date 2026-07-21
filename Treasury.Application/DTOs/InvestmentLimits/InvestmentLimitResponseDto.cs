namespace Treasury.Application.DTOs.InvestmentLimits;

public class InvestmentLimitResponseDto
{
    public Guid Id { get; set; }

    public Guid CounterpartyId { get; set; }

    public string CounterpartyCode { get; set; } =
        string.Empty;

    public string CounterpartyName { get; set; } =
        string.Empty;

    public string Currency { get; set; } =
        string.Empty;

    public string InvestmentType { get; set; } =
        string.Empty;

    public decimal MaximumExposureAmount { get; set; }

    public decimal WarningThresholdPercentage
        { get; set; }

    public DateTime EffectiveFromUtc { get; set; }

    public DateTime? EffectiveToUtc { get; set; }

    public bool IsActive { get; set; }

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}