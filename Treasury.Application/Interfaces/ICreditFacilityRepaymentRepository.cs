using Treasury.Application.DTOs.CreditFacilityRepayments;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface ICreditFacilityRepaymentRepository
{
    Task Add(
        CreditFacilityRepayment repayment);

    Task<CreditFacilityRepayment?> GetById(
        Guid id);

    Task<CreditFacilityRepayment?>
        GetByIdempotencyKey(
            string idempotencyKey);

    Task<bool> ReferenceExists(
        string reference);

    Task<(
        IReadOnlyList<CreditFacilityRepayment> Items,
        int TotalCount)> Search(
            Guid creditFacilityId,
            CreditFacilityRepaymentQueryDto query);
}