using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IOrganizationStructureRepository
{
    Task<Organization?> GetOrganization(
        Guid organizationId);

    Task<IReadOnlyList<LegalEntity>>
        GetLegalEntities();

    Task<LegalEntity?> GetLegalEntity(Guid id);

    Task<bool> LegalEntityCodeExists(
        string code);

    Task<bool> HasActiveBusinessUnits(
        Guid legalEntityId);

    Task AddLegalEntity(LegalEntity legalEntity);

    Task<IReadOnlyList<BusinessUnit>>
        GetBusinessUnits(Guid? legalEntityId);

    Task<BusinessUnit?> GetBusinessUnit(Guid id);

    Task<bool> BusinessUnitCodeExists(
        string code);

    Task AddBusinessUnit(BusinessUnit businessUnit);

    void SetOriginalConcurrencyToken(
        Organization organization,
        Guid concurrencyToken);

    void SetOriginalConcurrencyToken(
        LegalEntity legalEntity,
        Guid concurrencyToken);

    void SetOriginalConcurrencyToken(
        BusinessUnit businessUnit,
        Guid concurrencyToken);

    Task SaveChanges();
}
