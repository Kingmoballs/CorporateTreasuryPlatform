using Treasury.Application.DTOs.Approvals;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;

namespace Treasury.Infrastructure.Services;

public class ApprovalHistoryService
    : IApprovalHistoryService
{
    private readonly IApprovalDecisionRepository
        _decisionRepository;

    public ApprovalHistoryService(
        IApprovalDecisionRepository
            decisionRepository)
    {
        _decisionRepository =
            decisionRepository;
    }

    public async Task<List<ApprovalDecisionDto>>
        GetTransferHistory(Guid requestId)
    {
        return Map(
            await _decisionRepository
                .GetForTransfer(requestId));
    }

    public async Task<List<ApprovalDecisionDto>>
        GetPaymentHistory(Guid requestId)
    {
        return Map(
            await _decisionRepository
                .GetForPayment(requestId));
    }

    public async Task<List<ApprovalDecisionDto>>
        GetReversalHistory(Guid requestId)
    {
        return Map(
            await _decisionRepository
                .GetForReversal(requestId));
    }

    public async Task<List<ApprovalDecisionDto>>
        GetInvestmentPlacementHistory(
            Guid investmentPlacementId)
    {
        return Map(
            await _decisionRepository
                .GetForInvestmentPlacement(
                    investmentPlacementId));
    }

    private static List<ApprovalDecisionDto>
        Map(IEnumerable<ApprovalDecision> decisions)
    {
        return decisions
            .Select(decision =>
                new ApprovalDecisionDto
                {
                    Id =
                        decision.Id,

                    ApproverUserId =
                        decision.ApproverUserId,

                    ApproverName =
                        $"{decision.Approver.FirstName} " +
                        $"{decision.Approver.LastName}",

                    ApproverEmail =
                        decision.Approver.Email,

                    Decision =
                        decision.Decision,

                    Comment =
                        decision.Comment,

                    CreatedAtUtc =
                        decision.CreatedAtUtc
                })
            .ToList();
    }
}