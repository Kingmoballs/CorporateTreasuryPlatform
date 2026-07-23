using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly TreasuryDbContext _context;

    private readonly IOrganizationContext
        _organizationContext;

    public UserRepository(
        TreasuryDbContext context,
        IOrganizationContext organizationContext)
    {
        _context = context;

        _organizationContext =
            organizationContext;
    }

    public async Task<User?>
        GetByEmail(string email)
    {
        return await _context.Users
            .Include(x => x.Role)
            .Include(user =>
                user.OrganizationMemberships)
                .ThenInclude(membership =>
                    membership.Organization)
            .Include(user =>
                user.OrganizationMemberships)
                .ThenInclude(membership =>
                    membership.Role)
            .FirstOrDefaultAsync(
                x => x.Email == email);
    }

    public async Task<User?>
        GetById(Guid id)
    {
        var query =
            ApplyOrganizationScope(
                _context.Users);

        return await query
            .Include(x=>x.Role)
            .Include(user =>
                user.OrganizationMemberships)
                .ThenInclude(membership =>
                    membership.Organization)
            .Include(user =>
                user.OrganizationMemberships)
                .ThenInclude(membership =>
                    membership.Role)
            .FirstOrDefaultAsync(
                x=>x.Id==id);
    }

    public async Task Add(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public async Task<List<User>> GetAll()
    {
        var query =
            ApplyOrganizationScope(
                _context.Users.AsNoTracking());

        return await query
            .Include(user => user.Role)
            .Include(user =>
                user.OrganizationMemberships)
                .ThenInclude(membership =>
                    membership.Organization)
            .Include(user =>
                user.OrganizationMemberships)
                .ThenInclude(membership =>
                    membership.Role)
            .OrderBy(user => user.Email)
            .ToListAsync();
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }

    private IQueryable<User>
        ApplyOrganizationScope(
            IQueryable<User> query)
    {
        if (_organizationContext.IsSystemScope)
        {
            return query;
        }

        var organizationId =
            _organizationContext.OrganizationId;

        if (!organizationId.HasValue ||
            organizationId.Value == Guid.Empty)
        {
            return query.Where(_ => false);
        }

        return query.Where(user =>
            user.OrganizationMemberships.Any(
                membership =>
                    membership.OrganizationId ==
                        organizationId.Value));
    }
}
