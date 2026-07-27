namespace Treasury.Domain.Entities;

/*
 * A non-posting upload of pre-platform financial data.
 * Rows remain staged until a separate, explicitly approved
 * posting workflow is implemented.
 */
public class HistoricalTransactionImportBatch
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public Guid ImportKey { get; set; }

    public string Mode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string FileHash { get; set; } = string.Empty;

    public int TotalRowCount { get; set; }

    public int ValidRowCount { get; set; }

    public int InvalidRowCount { get; set; }

    public Guid UploadedByUserId { get; set; }

    public User UploadedByUser { get; set; } = null!;

    public DateTime UploadedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime ValidatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public Guid? SubmittedByUserId { get; set; }

    public User? SubmittedByUser { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public int RequiredApprovalCount { get; set; }

    public int ApprovalCount { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public Guid? RejectedByUserId { get; set; }

    public User? RejectedByUser { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    public Guid? CommittedByUserId { get; set; }

    public User? CommittedByUser { get; set; }

    public DateTime? CommittedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();

    public ICollection<HistoricalTransactionImportRow>
        Rows { get; set; } =
            new List<HistoricalTransactionImportRow>();

    public ICollection<
        HistoricalTransactionImportDecision>
        Decisions { get; set; } =
            new List<
                HistoricalTransactionImportDecision>();
}
