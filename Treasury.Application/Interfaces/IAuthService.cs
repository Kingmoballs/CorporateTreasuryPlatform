using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto>
        Register(RegisterDto dto);

    Task<AuthResponseDto>
        Login(LoginDto dto);
}