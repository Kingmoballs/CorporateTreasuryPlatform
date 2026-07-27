using Treasury.Application.DTOs.OrganizationOnboarding;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IOrganizationApplicationRepository
{
    Task<OrganizationApplication?> GetById(
        Guid applicationId);

    Task<OrganizationApplication?> GetBySubmissionKey(
        Guid submissionKey);

    Task<(IReadOnlyList<OrganizationApplication> Items,
        int TotalCount)> Search(
            OrganizationApplicationQueryDto query);

    Task<Organization?> GetPlatformOrganization();

    Task<Role?> GetRoleByName(string name);

    Task<bool> OrganizationCodeExists(string code);

    Task<bool> OrganizationSlugExists(string slug);

    Task<bool> UserHasRole(
        string normalizedEmail,
        string roleName);

    Task<bool> HasOpenApplication(
        string normalizedOrganizationName,
        string normalizedAdminEmail);

    Task AddApplication(
        OrganizationApplication application);

    Task AddProvisioning(
        Organization organization,
        LegalEntity legalEntity,
        BusinessUnit businessUnit,
        IReadOnlyCollection<ApprovalPolicy>
            approvalPolicies,
        UserInvitation invitation);

    Task AddAuditLog(AuditLog auditLog);

    void SetOriginalConcurrencyToken(
        OrganizationApplication application,
        Guid concurrencyToken);

    Task SaveChanges();
}
