using Treasury.Application.DTOs.InvestmentLimits;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IInvestmentLimitRepository
{
    Task Add(InvestmentLimit investmentLimit);

    Task<InvestmentLimit?> GetById(Guid id);

    Task<(
        IReadOnlyList<InvestmentLimit> Items,
        int TotalCount)> Search(
            InvestmentLimitQueryDto query);

    Task<bool> HasOverlappingActiveLimit(
        Guid counterpartyId,
        string currency,
        string investmentType,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        Guid? excludedLimitId);

    Task<IReadOnlyList<InvestmentLimit>>
        GetApplicableActiveLimits(
            Guid? counterpartyId,
            string? currency,
            DateTime asOfUtc);
    
    Task<IReadOnlyList<InvestmentLimit>>
        GetApplicableActiveLimitsForUpdate(
            Guid counterpartyId,
            string currency,
            string investmentType,
            DateTime asOfUtc);

    void Update(InvestmentLimit investmentLimit);

    Task SaveChanges();
}