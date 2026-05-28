using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
}