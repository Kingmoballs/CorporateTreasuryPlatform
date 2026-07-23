using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class OrganizationAccessRepository
    : IOrganizationAccessRepository
{
    private readonly TreasuryDbContext _context;

    public OrganizationAccessRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<
        OrganizationMembership>>
        GetActiveMembershipsForUser(Guid userId)
    {
        /*
         * This intentionally crosses the current tenant
         * filter, but remains bounded to the authenticated
         * user's immutable identifier.
         */
        return await BaseQuery()
            .Where(membership =>
                membership.UserId == userId &&
                membership.IsActive &&
                membership.Organization.IsActive &&
                membership.User.IsActive &&
                membership.User
                    .EmailVerifiedAtUtc.HasValue)
            .OrderByDescending(membership =>
                membership.IsDefault)
            .ThenBy(membership =>
                membership.Organization.Name)
            .ToListAsync();
    }

    public Task<OrganizationMembership?>
        GetActiveMembershipForUser(
            Guid organizationMembershipId,
            Guid userId)
    {
        return BaseQuery()
            .FirstOrDefaultAsync(membership =>
                membership.Id ==
                    organizationMembershipId &&
                membership.UserId == userId &&
                membership.IsActive &&
                membership.Organization.IsActive &&
                membership.User.IsActive &&
                membership.User
                    .EmailVerifiedAtUtc.HasValue);
    }

    private IQueryable<OrganizationMembership>
        BaseQuery()
    {
        return _context.OrganizationMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(membership =>
                membership.Organization)
            .Include(membership =>
                membership.Role)
            .Include(membership =>
                membership.User);
    }
}
