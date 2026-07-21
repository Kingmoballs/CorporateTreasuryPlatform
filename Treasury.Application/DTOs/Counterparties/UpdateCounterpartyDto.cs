namespace Treasury.Application.DTOs.Counterparties;

public class UpdateCounterpartyDto
{
    public string Name { get; set; } = string.Empty;

    public string CounterpartyType { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public string? SwiftCode { get; set; }

    public string? CreditRating { get; set; }

    public string? Notes { get; set; }
}