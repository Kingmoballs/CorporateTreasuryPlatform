using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IInvestmentEarlyRedemptionRequestRepository
{
    Task Add(
        InvestmentEarlyRedemptionRequest request);

    Task<InvestmentEarlyRedemptionRequest?> GetById(
        Guid id);

    Task<InvestmentEarlyRedemptionRequest?>
        GetByIdempotencyKey(
            string idempotencyKey);

    Task<List<InvestmentEarlyRedemptionRequest>>
        GetPending();

    Task<bool> HasDecision(
        Guid requestId,
        Guid approverUserId);

    Task AddDecision(
        InvestmentEarlyRedemptionDecision decision);

    void Update(
        InvestmentEarlyRedemptionRequest request);

    Task SaveChanges();
}