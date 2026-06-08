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

    [HttpPost("register")]
    public async Task<IActionResult>
        Register(RegisterDto dto)
    {
        var result =
            await _authService
                .Register(dto);

        return Ok(result);
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