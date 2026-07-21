namespace Treasury.Application.DTOs.Counterparties;

public class CounterpartyResponseDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string CounterpartyType { get; set; } =
        string.Empty;

    public string CountryCode { get; set; } =
        string.Empty;

    public string? SwiftCode { get; set; }

    public string? CreditRating { get; set; }

    public bool IsActive { get; set; }

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}