namespace Treasury.Application.DTOs.Fx;

public class FxRateResponseDto
{
    public Guid Id { get; set; }

    public string FromCurrency { get; set; } = string.Empty;

    public string ToCurrency { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public DateTime RateDateUtc { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public string? SourceReference { get; set; }

    public bool IsActive { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}