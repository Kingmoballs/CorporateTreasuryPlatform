namespace Treasury.Application.DTOs.BankStatements;

public class BookSideExceptionReportDto
{
    public Guid ImportId { get; set; }

    public Guid AccountId { get; set; }

    public string AccountName { get; set; } = string.Empty;

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string? StatementReference { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime? StatementFromUtc { get; set; }

    public DateTime? StatementToUtc { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public int UnmatchedTransactionCount { get; set; }

    public decimal NetUnmatchedAmount { get; set; }

    public decimal TotalUnmatchedInflowAmount { get; set; }

    public decimal TotalUnmatchedOutflowAmount { get; set; }

    public List<UnmatchedTreasuryTransactionDto> Transactions { get; set; }
        = new();
}
