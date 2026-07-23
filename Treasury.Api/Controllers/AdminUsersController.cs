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

    private readonly IUserInvitationService
        _invitationService;

    public AdminUsersController(
        IUserAdministrationService
            administrationService,
        IUserInvitationService
            invitationService)
    {
        _administrationService =
            administrationService;

        _invitationService =
            invitationService;
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

    [HttpPost("invitations")]
    public async Task<IActionResult>
        InviteUser(
            CreateUserInvitationDto dto)
    {
        var result =
            await _invitationService.Invite(dto);

        return CreatedAtAction(
            nameof(GetInvitations),
            new { },
            result);
    }

    [HttpGet("invitations")]
    public async Task<IActionResult>
        GetInvitations()
    {
        var result =
            await _invitationService
                .GetPending();

        return Ok(result);
    }

    [HttpPost(
        "invitations/{invitationId}/resend")]
    public async Task<IActionResult>
        ResendInvitation(Guid invitationId)
    {
        var result =
            await _invitationService.Resend(
                invitationId);

        return Ok(result);
    }

    [HttpDelete(
        "invitations/{invitationId}")]
    public async Task<IActionResult>
        RevokeInvitation(Guid invitationId)
    {
        await _invitationService.Revoke(
            invitationId);

        return NoContent();
    }
}
