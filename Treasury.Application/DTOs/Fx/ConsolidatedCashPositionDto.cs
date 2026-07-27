namespace Treasury.Application.DTOs.Fx;

public class ConsolidatedCashPositionDto
{
    public string BaseCurrency { get; set; } = string.Empty;

    public DateTime AsOfUtc { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public int AccountCount { get; set; }

    public decimal TotalBalanceInBaseCurrency { get; set; }

    public decimal TotalAvailableBalanceInBaseCurrency { get; set; }

    public decimal TotalReservedBalanceInBaseCurrency { get; set; }

    public List<ConsolidatedCashPositionAccountDto> Accounts { get; set; }
        = new();
}
