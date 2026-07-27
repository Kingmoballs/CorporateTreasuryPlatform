namespace Treasury.Application.DTOs.Accounts;

public class CreateAccountDto
{
    public string Name { get; set; }
        = string.Empty;

    public string AccountNumber { get; set; }
        = string.Empty;

    public Guid AccountTypeId { get; set; }

    public Guid LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public string Currency { get; set; }
        = "NGN";

    public decimal OpeningBalance { get; set; }
}
