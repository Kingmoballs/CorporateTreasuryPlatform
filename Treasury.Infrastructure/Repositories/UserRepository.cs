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
        var normalizedEmail =
            email.Trim().ToLowerInvariant();

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
                x => x.Email.ToLower() ==
                    normalizedEmail);
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

    public async Task RecordFailedLogin(
        Guid userId,
        DateTime failedAtUtc,
        DateTime failureWindowThresholdUtc,
        int maximumFailedAttempts,
        DateTime lockoutEndUtc)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        await LockUser(userId);

        var user =
            await _context.Users
                .FirstOrDefaultAsync(item =>
                    item.Id == userId);

        if (user is null)
        {
            await transaction.RollbackAsync();

            return;
        }

        /*
         * GetByEmail may already be tracking this user.
         * Reload after acquiring the row lock so the
         * calculation always uses the latest committed
         * authentication state.
         */
        await _context.Entry(user)
            .ReloadAsync();

        if (user.LoginLockoutEndUtc >
            failedAtUtc)
        {
            await transaction.CommitAsync();

            return;
        }

        var startsNewWindow =
            !user
                .LoginFailureWindowStartedAtUtc
                .HasValue ||
            user.LoginFailureWindowStartedAtUtc <=
                failureWindowThresholdUtc ||
            user.LoginLockoutEndUtc.HasValue;

        if (startsNewWindow)
        {
            user.FailedLoginAttempts = 1;

            user.LoginFailureWindowStartedAtUtc =
                failedAtUtc;

            user.LoginLockoutEndUtc = null;
        }
        else
        {
            user.FailedLoginAttempts++;
        }

        user.LastFailedLoginAtUtc = failedAtUtc;

        if (user.FailedLoginAttempts >=
            maximumFailedAttempts)
        {
            user.FailedLoginAttempts =
                maximumFailedAttempts;

            user.LoginLockoutEndUtc =
                lockoutEndUtc;
        }

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();
    }

    public async Task<bool>
        ClearFailedLoginsIfNotLocked(
            Guid userId,
            DateTime nowUtc)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        await LockUser(userId);

        var user =
            await _context.Users
                .FirstOrDefaultAsync(item =>
                    item.Id == userId);

        if (user is null)
        {
            await transaction.RollbackAsync();

            return false;
        }

        await _context.Entry(user)
            .ReloadAsync();

        if (user.LoginLockoutEndUtc > nowUtc)
        {
            await transaction.CommitAsync();

            return false;
        }

        user.FailedLoginAttempts = 0;
        user.LoginFailureWindowStartedAtUtc = null;
        user.LastFailedLoginAtUtc = null;
        user.LoginLockoutEndUtc = null;

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return true;
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }

    private Task LockUser(Guid userId)
    {
        /*
         * Serialize authentication-state changes for this
         * account across all API instances.
         */
        return _context.Database
            .ExecuteSqlInterpolatedAsync(
                $"""
                SELECT 1
                FROM "Users"
                WHERE "Id" = {userId}
                FOR UPDATE
                """);
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
