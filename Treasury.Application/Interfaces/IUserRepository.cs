using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmail(string email);

    Task<User?> GetById(Guid id);

    Task Add(User user);

    Task SaveChanges();
}