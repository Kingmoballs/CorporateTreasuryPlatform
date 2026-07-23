using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class UserInvitationRepository
    : IUserInvitationRepository
{
    private readonly TreasuryDbContext _context;

    public UserInvitationRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public Task<UserInvitation?>
        GetActiveForEmail(
            Guid organizationId,
            string normalizedEmail)
    {
        return BaseQuery()
            .FirstOrDefaultAsync(invitation =>
                invitation.OrganizationId ==
                    organizationId &&
                invitation.Email ==
                    normalizedEmail &&
                invitation.AcceptedAtUtc == null &&
                invitation.RevokedAtUtc == null);
    }

    public Task<UserInvitation?> GetById(
        Guid organizationId,
        Guid invitationId)
    {
        return BaseQuery()
            .FirstOrDefaultAsync(invitation =>
                invitation.OrganizationId ==
                    organizationId &&
                invitation.Id == invitationId);
    }

    public Task<UserInvitation?> GetByTokenHash(
        string tokenHash)
    {
        /*
         * A token lookup intentionally has no organization
         * predicate. The recipient has not authenticated;
         * the 256-bit token is the authorization secret.
         */
        return BaseQuery()
            .FirstOrDefaultAsync(invitation =>
                invitation.TokenHash == tokenHash);
    }

    public Task<List<UserInvitation>> GetPending(
        Guid organizationId)
    {
        return BaseQuery()
            .AsNoTracking()
            .Where(invitation =>
                invitation.OrganizationId ==
                    organizationId &&
                invitation.AcceptedAtUtc == null &&
                invitation.RevokedAtUtc == null)
            .OrderByDescending(invitation =>
                invitation.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task Add(
        UserInvitation invitation)
    {
        await _context.UserInvitations
            .AddAsync(invitation);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }

    private IQueryable<UserInvitation> BaseQuery()
    {
        return _context.UserInvitations
            .Include(invitation =>
                invitation.Organization)
            .Include(invitation =>
                invitation.Role);
    }
}
