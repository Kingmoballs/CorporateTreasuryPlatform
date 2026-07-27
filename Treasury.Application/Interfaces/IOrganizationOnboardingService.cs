using Treasury.Application.DTOs.OrganizationOnboarding;

namespace Treasury.Application.Interfaces;

public interface IOrganizationOnboardingService
{
    Task<OrganizationApplicationResponseDto> Submit(
        SubmitOrganizationApplicationDto dto,
        Guid submissionKey);

    Task<PagedOrganizationApplicationsDto> Search(
        OrganizationApplicationQueryDto query);

    Task<OrganizationApplicationResponseDto> GetById(
        Guid applicationId);

    Task<OrganizationApplicationResponseDto> BeginReview(
        Guid applicationId,
        ReviewOrganizationApplicationDto dto);

    Task<OrganizationApplicationApprovalResponseDto>
        Approve(
            Guid applicationId,
            ApproveOrganizationApplicationDto dto);

    Task<OrganizationApplicationResponseDto> Reject(
        Guid applicationId,
        RejectOrganizationApplicationDto dto);

    Task<AdminInvitationDeliveryResponseDto>
        ResendAdminInvitation(Guid applicationId);
}
