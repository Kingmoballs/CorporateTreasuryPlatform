using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByName(string name);

    Task<bool> RoleExists(string name);

    Task Add(Role role);

    Task SaveChanges();
}

