using Treasury.Application.DTOs.CreditFacilityAccruals;

namespace Treasury.Application.Interfaces;

public interface ICreditFacilityInterestAccrualService
{
    Task<CreditFacilityAccrualGenerationResultDto>
        Generate(
            GenerateCreditFacilityAccrualsDto dto);

    Task<PagedCreditFacilityAccrualSnapshotResponseDto>
        Search(
            CreditFacilityAccrualSnapshotQueryDto query);
}