namespace Treasury.Application.DTOs.BankStatements;

public class BankStatementReconciliationResultDto
{
    public Guid ImportId { get; set; }

    public Guid AccountId { get; set; }

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public DateTime ProcessedAtUtc { get; set; }

    public int CandidateLineCount { get; set; }

    public int MatchedLineCount { get; set; }

    public int UnmatchedLineCount { get; set; }

    public int AmbiguousMatchCount { get; set; }

    public List<Guid> MatchedLineIds { get; set; }
        = new();
}
