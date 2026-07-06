using Treasury.Application.DTOs.Approvals;

namespace Treasury.Application.Interfaces;

public interface IPendingRequestExpiryService
{
    Task<PendingRequestExpiryResultDto>
        ExpireDueRequests(
            CancellationToken cancellationToken =
                default);
}