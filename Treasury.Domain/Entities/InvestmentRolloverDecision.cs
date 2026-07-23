namespace Treasury.Domain.Entities;

public class InvestmentRolloverDecision
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid InvestmentRolloverRequestId
        { get; set; }

    public InvestmentRolloverRequest
        InvestmentRolloverRequest { get; set; } =
        null!;

    public Guid ApproverUserId { get; set; }

    public User ApproverUser { get; set; } = null!;

    public string Decision { get; set; } =
        string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}
