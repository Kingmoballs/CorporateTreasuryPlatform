using Treasury.Application.DTOs.Auth;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IAuthenticationSecurityEventRepository
{
    Task Add(AuthenticationSecurityEvent item);

    Task<(
        IReadOnlyList<AuthenticationSecurityEvent>
            Items,
        int TotalCount)> Search(
            Guid organizationId,
            AuthenticationSecurityEventQueryDto query);

    Task<int> DeleteOlderThan(
        DateTime cutoffUtc,
        int batchSize);

    Task SaveChanges();
}
