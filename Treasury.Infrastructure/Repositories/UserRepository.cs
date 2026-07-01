using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly TreasuryDbContext _context;

    public UserRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task<User?>
        GetByEmail(string email)
    {
        return await _context.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Email == email);
    }

    public async Task<User?>
        GetById(Guid id)
    {
        return await _context.Users
            .Include(x=>x.Role)
            .FirstOrDefaultAsync(
                x=>x.Id==id);
    }

    public async Task Add(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public async Task<List<User>> GetAll()
    {
        return await _context.Users
            .AsNoTracking()
            .Include(user => user.Role)
            .OrderBy(user => user.Email)
            .ToListAsync();
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}