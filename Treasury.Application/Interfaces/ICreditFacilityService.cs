using Treasury.Application.DTOs.CreditFacilities;

namespace Treasury.Application.Interfaces;

public interface ICreditFacilityService
{
    Task<CreditFacilityResponseDto> Create(
        CreateCreditFacilityDto dto);

    Task<CreditFacilityResponseDto> GetById(
        Guid id);

    Task<PagedCreditFacilityResponseDto> Search(
        CreditFacilityQueryDto query);

    Task<CreditFacilityResponseDto> Update(
        Guid id,
        UpdateCreditFacilityDto dto);

    Task<CreditFacilityResponseDto> Activate(
        Guid id,
        string idempotencyKey);

    Task<CreditFacilityResponseDto>
        ApproveActivation(Guid id);

    Task<CreditFacilityResponseDto>
        RejectActivation(
            Guid id,
            string reason);

    Task<CreditFacilityResponseDto> Cancel(
        Guid id,
        string reason);
}