using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;

namespace Treasury.Api.Controllers;

[ApiController]

[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService
        _authService;

    public AuthController(
        IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult>
        Login(LoginDto dto)
    {
        var result =
            await _authService
                .Login(dto);

        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult>
        Refresh(RefreshTokenDto dto)
    {
        var result =
            await _authService.Refresh(dto);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _authService.Logout();

        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        await _authService.LogoutAll();

        return NoContent();
    }

    [HttpPost("invitations/accept")]
    public async Task<IActionResult>
        AcceptInvitation(
            AcceptUserInvitationDto dto,
            [FromServices]
            IUserInvitationService
                invitationService)
    {
        var result =
            await invitationService.Accept(dto);

        return Ok(result);
    }

    [HttpPost("password/forgot")]
    public async Task<IActionResult>
        ForgotPassword(
            ForgotPasswordDto dto,
            [FromServices]
            IPasswordRecoveryService
                passwordRecoveryService)
    {
        var result =
            await passwordRecoveryService
                .RequestReset(dto);

        return Accepted(result);
    }

    [HttpPost("password/reset")]
    public async Task<IActionResult>
        ResetPassword(
            ResetPasswordDto dto,
            [FromServices]
            IPasswordRecoveryService
                passwordRecoveryService)
    {
        await passwordRecoveryService
            .ResetPassword(dto);

        return NoContent();
    }

    [Authorize]

    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            User = User.Identity!.Name,

            Claims = User.Claims.Select(x =>
                new
                {
                    x.Type,
                    x.Value
                })
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin-test")]
    public IActionResult AdminOnly()
    {
        return Ok("Admin Access Granted");
    }
}
