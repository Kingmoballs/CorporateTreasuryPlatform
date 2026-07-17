namespace Treasury.Domain.Entities;

public class InvestmentEarlyRedemptionDecision
{
    public Guid Id { get; set; }

    public Guid InvestmentEarlyRedemptionRequestId
        { get; set; }

    public InvestmentEarlyRedemptionRequest
        InvestmentEarlyRedemptionRequest
        { get; set; } = null!;

    public Guid ApproverUserId { get; set; }

    public User ApproverUser { get; set; } = null!;

    public string Decision { get; set; } =
        string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}