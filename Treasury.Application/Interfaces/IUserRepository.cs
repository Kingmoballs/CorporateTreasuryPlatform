using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmail(string email);

    Task<User?> GetById(Guid id);

    Task Add(User user);

    Task<List<User>> GetAll();

    Task RecordFailedLogin(
        Guid userId,
        DateTime failedAtUtc,
        DateTime failureWindowThresholdUtc,
        int maximumFailedAttempts,
        DateTime lockoutEndUtc);

    Task<bool> ClearFailedLoginsIfNotLocked(
        Guid userId,
        DateTime nowUtc);

    Task SaveChanges();
}
