namespace Treasury.Domain.Entities;

/*
 * Role is snapshotted because cutover approval requires
 * evidence that one Admin and one CFO approved the batch.
 */
public class HistoricalTransactionImportDecision
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public Guid BatchId { get; set; }

    public HistoricalTransactionImportBatch Batch
        { get; set; } = null!;

    public Guid ApproverUserId { get; set; }

    public User ApproverUser { get; set; } = null!;

    public string ApproverRole { get; set; } =
        string.Empty;

    public string Decision { get; set; } =
        string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}
