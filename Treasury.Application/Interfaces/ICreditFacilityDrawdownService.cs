using Treasury.Application.DTOs.CreditFacilityDrawdowns;

namespace Treasury.Application.Interfaces;

public interface ICreditFacilityDrawdownService
{
    Task<CreditFacilityDrawdownResponseDto> Execute(
        Guid creditFacilityId,
        CreateCreditFacilityDrawdownDto dto);

    Task<CreditFacilityDrawdownResponseDto> GetById(
        Guid id);

    Task<PagedCreditFacilityDrawdownResponseDto>
        Search(
            Guid creditFacilityId,
            CreditFacilityDrawdownQueryDto query);
}