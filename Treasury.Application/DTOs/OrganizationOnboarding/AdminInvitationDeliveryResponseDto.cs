namespace Treasury.Application.DTOs.OrganizationOnboarding;

public class AdminInvitationDeliveryResponseDto
{
    public Guid InvitationId { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public bool EmailSent { get; set; }

    public string? ManualInvitationAcceptanceUrl
        { get; set; }
}
