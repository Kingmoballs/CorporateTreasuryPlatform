using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Interfaces;

public interface IAuthenticationSecurityEventService
{
    Task Record(
        RecordAuthenticationSecurityEventDto dto);

    Task<PagedAuthenticationSecurityEventResponseDto>
        Search(
            AuthenticationSecurityEventQueryDto query);

    Task<int> DeleteOlderThan(
        DateTime cutoffUtc,
        int batchSize);
}
