using Treasury.Application.DTOs.Approvals;

namespace Treasury.Application.Interfaces;

public interface IApprovalHistoryService
{
    Task<List<ApprovalDecisionDto>>
        GetTransferHistory(Guid requestId);

    Task<List<ApprovalDecisionDto>>
        GetPaymentHistory(Guid requestId);

    Task<List<ApprovalDecisionDto>>
        GetReversalHistory(Guid requestId);
}