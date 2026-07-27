namespace Treasury.Application.DTOs.BankStatements;

public class UnmatchedBankStatementLinesQueryDto
{
    public Guid? AccountId { get; set; }

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}
