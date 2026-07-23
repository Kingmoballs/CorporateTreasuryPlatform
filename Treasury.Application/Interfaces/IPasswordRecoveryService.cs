using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Interfaces;

public interface IPasswordRecoveryService
{
    Task<ForgotPasswordResponseDto> RequestReset(
        ForgotPasswordDto dto);

    Task ResetPassword(ResetPasswordDto dto);
}
