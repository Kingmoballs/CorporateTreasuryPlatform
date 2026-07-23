using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IOrganizationAccessRepository
{
    Task<IReadOnlyList<OrganizationMembership>>
        GetActiveMembershipsForUser(Guid userId);

    Task<OrganizationMembership?>
        GetActiveMembershipForUser(
            Guid organizationMembershipId,
            Guid userId);
}
