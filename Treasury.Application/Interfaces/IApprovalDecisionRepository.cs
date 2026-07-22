using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IApprovalDecisionRepository
{
    Task Add(ApprovalDecision decision);

    Task<bool> HasTransferDecision(
        Guid transferRequestId,
        Guid approverUserId);

    Task<bool> HasPaymentDecision(
        Guid paymentRequestId,
        Guid approverUserId);

    Task<bool> HasReversalDecision(
        Guid reversalRequestId,
        Guid approverUserId);

    Task<List<ApprovalDecision>>
        GetForTransfer(Guid transferRequestId);

    Task<List<ApprovalDecision>>
        GetForPayment(Guid paymentRequestId);

    Task<List<ApprovalDecision>>
        GetForReversal(Guid reversalRequestId);

    Task<bool> HasInvestmentPlacementDecision(
        Guid investmentPlacementId,
        Guid approverUserId);

    Task<List<ApprovalDecision>>
        GetForInvestmentPlacement(
            Guid investmentPlacementId);

    Task<bool> HasCreditFacilityDecision(
        Guid creditFacilityId,
        Guid approverUserId);

    Task<List<ApprovalDecision>>
        GetForCreditFacility(
            Guid creditFacilityId);
}