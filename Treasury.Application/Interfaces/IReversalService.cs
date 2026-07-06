using Treasury.Application.DTOs.Reversals;
using Treasury.Application.DTOs.Transactions;

namespace Treasury.Application.Interfaces;

public interface IReversalService
{
    Task<ReversalRequestResponseDto>
        RequestReversal(
            string transactionReference,
            string reason);

    Task<List<ReversalRequestResponseDto>>
        GetPending();

    Task<ReversalApprovalResponseDto>
        Approve(Guid reversalRequestId);

    Task<ReversalRequestResponseDto>
        Reject(
            Guid reversalRequestId,
            string reason);
}