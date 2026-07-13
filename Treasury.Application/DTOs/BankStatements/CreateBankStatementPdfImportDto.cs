namespace Treasury.Application.DTOs.BankStatements;

public class CreateBankStatementPdfImportDto
{
    public Guid AccountId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public byte[] PdfContent { get; set; } = Array.Empty<byte>();

    public string? StatementReference { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime? StatementFromUtc { get; set; }

    public DateTime? StatementToUtc { get; set; }

    public decimal? OpeningBalance { get; set; }

    public decimal? ClosingBalance { get; set; }
}