namespace Treasury.Application.DTOs.BankStatements;

public class BankStatementExceptionReportDto
{
    public Guid ImportId { get; set; }

    public Guid AccountId { get; set; }

    public string AccountName { get; set; } = string.Empty;

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string? StatementReference { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }

    public int ActionRequiredLineCount { get; set; }

    public int UnmatchedLineCount { get; set; }

    public int MatchedPendingReconciliationCount { get; set; }

    public List<BankStatementLineResponseDto> Lines { get; set; }
        = new();
}
