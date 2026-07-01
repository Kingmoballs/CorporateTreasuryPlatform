using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Admin;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/admin")]
[ApiController]
[Authorize(Roles = Roles.Admin)]
public class AdminUsersController
    : ControllerBase
{
    private readonly IUserAdministrationService
        _administrationService;

    public AdminUsersController(
        IUserAdministrationService
            administrationService)
    {
        _administrationService =
            administrationService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var result =
            await _administrationService
                .GetUsers();

        return Ok(result);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var result =
            await _administrationService
                .GetRoles();

        return Ok(result);
    }

    [HttpPatch("users/{userId}/role")]
    public async Task<IActionResult> AssignRole(
        Guid userId,
        UpdateUserRoleDto dto)
    {
        var result =
            await _administrationService
                .AssignRole(
                    userId,
                    dto.RoleId);

        return Ok(result);
    }

    [HttpPatch("users/{userId}/status")]
    public async Task<IActionResult>
        SetUserStatus(
            Guid userId,
            UpdateUserStatusDto dto)
    {
        var result =
            await _administrationService
                .SetUserStatus(
                    userId,
                    dto.IsActive);

        return Ok(result);
    }
}