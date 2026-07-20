namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentRolloverDecisionDto
{
    public Guid Id { get; set; }

    public Guid ApproverUserId { get; set; }

    public string Decision { get; set; } =
        string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}