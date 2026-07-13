namespace Treasury.Api.DTOs;

public class BankStatementPdfUploadRequest
{
    public Guid AccountId { get; set; }

    public IFormFile File { get; set; } = null!;

    public string? StatementReference { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime? StatementFromUtc { get; set; }

    public DateTime? StatementToUtc { get; set; }

    public decimal? OpeningBalance { get; set; }

    public decimal? ClosingBalance { get; set; }
}