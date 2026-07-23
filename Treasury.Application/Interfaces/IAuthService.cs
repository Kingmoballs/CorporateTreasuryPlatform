using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto>
        Login(LoginDto dto);

    Task<AuthResponseDto> Refresh(
        RefreshTokenDto dto);

    Task Logout();

    Task LogoutAll();
}
