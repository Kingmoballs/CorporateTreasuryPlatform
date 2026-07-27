namespace Treasury.Application.DTOs.Transactions;

public class TransactionQueryDto
{
    public string? Currency { get; set; }

    public string? Status { get; set; }

    public string? TransactionType { get; set; }

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
