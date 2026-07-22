namespace Treasury.Application.DTOs.CreditFacilityDrawdowns;

public class CreateCreditFacilityDrawdownDto
{
    public decimal Amount { get; set; }

    public string? ExternalReference { get; set; }

    public string IdempotencyKey { get; set; } =
        string.Empty;

    public string? Description { get; set; }
}