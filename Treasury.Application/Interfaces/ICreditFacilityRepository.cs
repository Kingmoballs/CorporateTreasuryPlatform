using Treasury.Application.DTOs.CreditFacilities;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface ICreditFacilityRepository
{
    Task Add(CreditFacility facility);

    Task<CreditFacility?> GetById(Guid id);

    Task<CreditFacility?>
        GetByActivationIdempotencyKey(
            string idempotencyKey);

    Task<bool> ReferenceExists(string reference);

    Task<(
        IReadOnlyList<CreditFacility> Items,
        int TotalCount)> Search(
            CreditFacilityQueryDto query);

    void Update(CreditFacility facility);

    Task SaveChanges();
}