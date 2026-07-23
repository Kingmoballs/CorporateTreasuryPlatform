using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class OrganizationRepository :
    IOrganizationRepository
{
    private readonly TreasuryDbContext _context;

    public OrganizationRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task<Organization?> GetById(
        Guid id)
    {
        return await _context.Organizations
            .FirstOrDefaultAsync(organization =>
                organization.Id == id);
    }

    public async Task<Organization?> GetByCode(
        string code)
    {
        return await _context.Organizations
            .FirstOrDefaultAsync(organization =>
                organization.Code == code);
    }
}
