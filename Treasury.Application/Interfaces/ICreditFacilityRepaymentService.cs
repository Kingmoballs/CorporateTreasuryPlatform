using Treasury.Application.DTOs.CreditFacilityRepayments;

namespace Treasury.Application.Interfaces;

public interface ICreditFacilityRepaymentService
{
    Task<CreditFacilityRepaymentResponseDto> Execute(
        Guid creditFacilityId,
        CreateCreditFacilityRepaymentDto dto);

    Task<CreditFacilityRepaymentResponseDto> GetById(
        Guid id);

    Task<PagedCreditFacilityRepaymentResponseDto>
        Search(
            Guid creditFacilityId,
            CreditFacilityRepaymentQueryDto query);
}