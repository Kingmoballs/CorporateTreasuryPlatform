namespace Treasury.Application.DTOs.BankStatements;

public class BankStatementImportResponseDto
{
    public Guid Id { get; set; }

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

    public int LineCount { get; set; }

    public Guid? UploadedByUserId { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    public List<BankStatementLineResponseDto> Lines { get; set; }
        = new();
}
