using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly TreasuryDbContext _context;

    public RoleRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task<Role?>
        GetByName(string name)
    {
        return await _context.Roles
            .FirstOrDefaultAsync(
                x => x.Name == name);
    }

    public async Task<bool>
        RoleExists(string name)
    {
        return await _context.Roles
            .AnyAsync(x => x.Name == name);
    }

    public async Task Add(Role role)
    {
        await _context.Roles.AddAsync(role);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}