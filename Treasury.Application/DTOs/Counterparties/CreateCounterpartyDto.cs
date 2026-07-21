namespace Treasury.Application.DTOs.Counterparties;

public class CreateCounterpartyDto
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string CounterpartyType { get; set; } = "Bank";

    public string CountryCode { get; set; } = "NG";

    public string? SwiftCode { get; set; }

    public string? CreditRating { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }
}