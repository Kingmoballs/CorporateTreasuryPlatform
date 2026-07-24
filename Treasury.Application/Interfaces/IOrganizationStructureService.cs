using Treasury.Application.DTOs.Organizations;

namespace Treasury.Application.Interfaces;

public interface IOrganizationStructureService
{
    Task<OrganizationProfileResponseDto>
        GetOrganization();

    Task<OrganizationProfileResponseDto>
        UpdateOrganization(
            UpdateOrganizationProfileDto dto);

    Task<IReadOnlyList<LegalEntityResponseDto>>
        GetLegalEntities();

    Task<LegalEntityResponseDto>
        CreateLegalEntity(CreateLegalEntityDto dto);

    Task<LegalEntityResponseDto>
        UpdateLegalEntity(
            Guid id,
            UpdateLegalEntityDto dto);

    Task<LegalEntityResponseDto>
        SetLegalEntityStatus(
            Guid id,
            UpdateOrganizationStructureStatusDto dto);

    Task<IReadOnlyList<BusinessUnitResponseDto>>
        GetBusinessUnits(Guid? legalEntityId);

    Task<BusinessUnitResponseDto>
        CreateBusinessUnit(CreateBusinessUnitDto dto);

    Task<BusinessUnitResponseDto>
        UpdateBusinessUnit(
            Guid id,
            UpdateBusinessUnitDto dto);

    Task<BusinessUnitResponseDto>
        SetBusinessUnitStatus(
            Guid id,
            UpdateOrganizationStructureStatusDto dto);
}
