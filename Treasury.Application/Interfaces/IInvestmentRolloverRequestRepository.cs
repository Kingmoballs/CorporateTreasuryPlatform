using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IInvestmentRolloverRequestRepository
{
    Task Add(InvestmentRolloverRequest request);

    Task<InvestmentRolloverRequest?> GetById(Guid id);

    Task<InvestmentRolloverRequest?> GetByIdempotencyKey(
        string idempotencyKey);

    Task<InvestmentRolloverRequest?> GetOpenForPlacement(
        Guid investmentPlacementId);

    Task<List<InvestmentRolloverRequest>> GetPending();

    Task<bool> HasDecision(
        Guid requestId,
        Guid approverUserId);

    Task AddDecision(
        InvestmentRolloverDecision decision);

    Task SaveChanges();
}