namespace Treasury.Application.DTOs.BankStatements;

public class BankStatementReconciliationSummaryDto
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

    public decimal? OpeningBalance { get; set; }

    public decimal? ClosingBalance { get; set; }

    public decimal NetStatementMovement { get; set; }

    public decimal TotalInflowAmount { get; set; }

    public decimal TotalOutflowAmount { get; set; }

    public decimal? CalculatedClosingBalance { get; set; }

    public decimal? ClosingBalanceVariance { get; set; }

    public int TotalLineCount { get; set; }

    public int UnmatchedLineCount { get; set; }

    public int MatchedLineCount { get; set; }

    public int ReconciledLineCount { get; set; }

    public int IgnoredLineCount { get; set; }

    public int MatchedButNotReconciledCount { get; set; }

    public int ActionRequiredLineCount { get; set; }

    public decimal ReconciliationCompletionPercentage { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}
