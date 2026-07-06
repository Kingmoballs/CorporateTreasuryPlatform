namespace Treasury.Domain.Entities;

public class BankStatementImport
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;

    public string? StatementReference { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime? StatementFromUtc { get; set; }

    public DateTime? StatementToUtc { get; set; }

    public decimal? OpeningBalance { get; set; }

    public decimal? ClosingBalance { get; set; }

    public int LineCount { get; set; }

    public Guid? UploadedByUserId { get; set; }

    public User? UploadedByUser { get; set; }

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<BankStatementLine> Lines { get; set; }
        = new List<BankStatementLine>();
}