using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class OrganizationStructureRepository
    : IOrganizationStructureRepository
{
    private readonly TreasuryDbContext _context;

    public OrganizationStructureRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public Task<Organization?> GetOrganization(
        Guid organizationId)
    {
        return _context.Organizations
            .FirstOrDefaultAsync(organization =>
                organization.Id ==
                    organizationId);
    }

    public async Task<IReadOnlyList<LegalEntity>>
        GetLegalEntities()
    {
        return await _context.LegalEntities
            .AsNoTracking()
            .OrderBy(entity => entity.Name)
            .ToListAsync();
    }

    public Task<LegalEntity?> GetLegalEntity(
        Guid id)
    {
        return _context.LegalEntities
            .FirstOrDefaultAsync(entity =>
                entity.Id == id);
    }

    public Task<bool> LegalEntityCodeExists(
        string code)
    {
        return _context.LegalEntities
            .AsNoTracking()
            .AnyAsync(entity =>
                entity.Code == code);
    }

    public Task<bool> HasActiveBusinessUnits(
        Guid legalEntityId)
    {
        return _context.BusinessUnits
            .AsNoTracking()
            .AnyAsync(unit =>
                unit.LegalEntityId ==
                    legalEntityId &&
                unit.IsActive);
    }

    public async Task AddLegalEntity(
        LegalEntity legalEntity)
    {
        await _context.LegalEntities
            .AddAsync(legalEntity);
    }

    public async Task<IReadOnlyList<BusinessUnit>>
        GetBusinessUnits(Guid? legalEntityId)
    {
        var query =
            _context.BusinessUnits
                .AsNoTracking()
                .Include(unit =>
                    unit.LegalEntity)
                .AsQueryable();

        if (legalEntityId.HasValue)
        {
            query = query.Where(unit =>
                unit.LegalEntityId ==
                    legalEntityId.Value);
        }

        return await query
            .OrderBy(unit => unit.Name)
            .ToListAsync();
    }

    public Task<BusinessUnit?> GetBusinessUnit(
        Guid id)
    {
        return _context.BusinessUnits
            .Include(unit => unit.LegalEntity)
            .FirstOrDefaultAsync(unit =>
                unit.Id == id);
    }

    public Task<bool> BusinessUnitCodeExists(
        string code)
    {
        return _context.BusinessUnits
            .AsNoTracking()
            .AnyAsync(unit =>
                unit.Code == code);
    }

    public async Task AddBusinessUnit(
        BusinessUnit businessUnit)
    {
        await _context.BusinessUnits
            .AddAsync(businessUnit);
    }

    public void SetOriginalConcurrencyToken(
        Organization organization,
        Guid concurrencyToken)
    {
        _context.Entry(organization)
            .Property(item =>
                item.ConcurrencyToken)
            .OriginalValue =
                concurrencyToken;
    }

    public void SetOriginalConcurrencyToken(
        LegalEntity legalEntity,
        Guid concurrencyToken)
    {
        _context.Entry(legalEntity)
            .Property(item =>
                item.ConcurrencyToken)
            .OriginalValue =
                concurrencyToken;
    }

    public void SetOriginalConcurrencyToken(
        BusinessUnit businessUnit,
        Guid concurrencyToken)
    {
        _context.Entry(businessUnit)
            .Property(item =>
                item.ConcurrencyToken)
            .OriginalValue =
                concurrencyToken;
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
