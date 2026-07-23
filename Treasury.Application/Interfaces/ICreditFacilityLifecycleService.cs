using Treasury.Application.DTOs.CreditFacilityLifecycle;

namespace Treasury.Application.Interfaces;

public interface ICreditFacilityLifecycleService
{
    Task<CreditFacilityLifecycleResponseDto> Suspend(
        Guid creditFacilityId,
        string reason);

    Task<CreditFacilityLifecycleResponseDto> Reactivate(
        Guid creditFacilityId,
        string reason);

    Task<CreditFacilityLifecycleResponseDto> Close(
        Guid creditFacilityId,
        string reason);

    Task<CreditFacilityMaturityProcessingResultDto>
        ProcessMaturities(
            ProcessCreditFacilityMaturitiesDto dto);
}