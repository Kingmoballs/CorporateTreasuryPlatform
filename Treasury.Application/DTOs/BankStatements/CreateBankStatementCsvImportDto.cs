namespace Treasury.Application.DTOs.BankStatements;

public class CreateBankStatementCsvImportDto
{
    public Guid AccountId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string CsvContent { get; set; } = string.Empty;

    public string? StatementReference { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime? StatementFromUtc { get; set; }

    public DateTime? StatementToUtc { get; set; }

    public decimal? OpeningBalance { get; set; }

    public decimal? ClosingBalance { get; set; }
}