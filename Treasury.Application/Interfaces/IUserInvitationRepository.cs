using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IUserInvitationRepository
{
    Task<UserInvitation?> GetActiveForEmail(
        Guid organizationId,
        string normalizedEmail);

    Task<UserInvitation?> GetById(
        Guid organizationId,
        Guid invitationId);

    Task<UserInvitation?> GetByTokenHash(
        string tokenHash);

    Task<List<UserInvitation>> GetPending(
        Guid organizationId);

    Task Add(UserInvitation invitation);

    Task SaveChanges();
}
