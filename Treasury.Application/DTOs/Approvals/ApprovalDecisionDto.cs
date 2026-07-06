namespace Treasury.Application.DTOs.Approvals;

public class ApprovalDecisionDto
{
    public Guid Id { get; set; }

    public Guid ApproverUserId { get; set; }

    public string ApproverName { get; set; }
        = string.Empty;

    public string ApproverEmail { get; set; }
        = string.Empty;

    public string Decision { get; set; }
        = string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}