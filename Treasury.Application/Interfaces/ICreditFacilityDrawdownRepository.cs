using Treasury.Application.DTOs.CreditFacilityDrawdowns;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface ICreditFacilityDrawdownRepository
{
    Task Add(
        CreditFacilityDrawdown drawdown);

    Task<CreditFacilityDrawdown?> GetById(
        Guid id);

    Task<CreditFacilityDrawdown?>
        GetByIdempotencyKey(
            string idempotencyKey);

    Task<bool> ReferenceExists(
        string reference);

    Task<(
        IReadOnlyList<CreditFacilityDrawdown> Items,
        int TotalCount)> Search(
            Guid creditFacilityId,
            CreditFacilityDrawdownQueryDto query);
}