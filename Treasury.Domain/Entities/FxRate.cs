namespace Treasury.Domain.Entities;

public class FxRate
{
    public Guid Id { get; set; }

    /*
     * Example:
     * FromCurrency = USD
     * ToCurrency = NGN
     * Rate = 1500
     *
     * Meaning: 1 USD = 1500 NGN.
     */
    public string FromCurrency { get; set; } = string.Empty;

    public string ToCurrency { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public DateTime RateDateUtc { get; set; }

    public string SourceType { get; set; } = "Manual";

    public string? SourceReference { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}