using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Treasury.Api.Security;
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
    [EnableRateLimiting(
        AuthenticationRateLimitPolicies.Login)]
    public async Task<IActionResult>
        Login(LoginDto dto)
    {
        var result =
            await _authService
                .Login(dto);

        return Ok(result);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(
        AuthenticationRateLimitPolicies.Refresh)]
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

    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(
        [FromServices]
        IAuthenticationSessionManagementService
            sessionManagementService)
    {
        var sessions =
            await sessionManagementService
                .GetActiveSessions();

        return Ok(sessions);
    }

    [Authorize]
    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(
        Guid sessionId,
        [FromServices]
        IAuthenticationSessionManagementService
            sessionManagementService)
    {
        await sessionManagementService
            .RevokeOwnedSession(sessionId);

        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-others")]
    public async Task<IActionResult> LogoutOthers(
        [FromServices]
        IAuthenticationSessionManagementService
            sessionManagementService)
    {
        await sessionManagementService
            .RevokeOtherSessions();

        return NoContent();
    }

    [Authorize]
    [HttpGet("organizations")]
    public async Task<IActionResult>
        GetAvailableOrganizations(
            [FromServices]
            IOrganizationAccessService
                organizationAccessService)
    {
        var result =
            await organizationAccessService
                .GetAvailableOrganizations();

        return Ok(result);
    }

    [Authorize]
    [HttpPost("organizations/switch")]
    [EnableRateLimiting(
        AuthenticationRateLimitPolicies.Refresh)]
    public async Task<IActionResult>
        SwitchOrganization(
            SwitchOrganizationDto dto,
            [FromServices]
            IOrganizationAccessService
                organizationAccessService)
    {
        var result =
            await organizationAccessService
                .SwitchOrganization(dto);

        return Ok(result);
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
    [EnableRateLimiting(
        AuthenticationRateLimitPolicies
            .PasswordRecovery)]
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
    [EnableRateLimiting(
        AuthenticationRateLimitPolicies
            .PasswordRecovery)]
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

    [HttpPost("mfa/challenges/verify")]
    [EnableRateLimiting(
        AuthenticationRateLimitPolicies
            .MultiFactorAuthentication)]
    public async Task<IActionResult>
        VerifyMfaChallenge(
            VerifyMfaChallengeDto dto,
            [FromServices]
            IMultiFactorAuthenticationService
                multiFactorService)
    {
        var result =
            await multiFactorService
                .VerifyChallenge(dto);

        return Ok(result);
    }

    [HttpPost("mfa/challenges/recovery-code")]
    [EnableRateLimiting(
        AuthenticationRateLimitPolicies
            .MultiFactorAuthentication)]
    public async Task<IActionResult>
        UseMfaRecoveryCode(
            UseMfaRecoveryCodeDto dto,
            [FromServices]
            IMultiFactorAuthenticationService
                multiFactorService)
    {
        var result =
            await multiFactorService
                .UseRecoveryCode(dto);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("mfa/enrollment/start")]
    [EnableRateLimiting(
        AuthenticationRateLimitPolicies
            .MultiFactorAuthentication)]
    public async Task<IActionResult>
        StartMfaEnrollment(
            StartMfaEnrollmentDto dto,
            [FromServices]
            IMultiFactorAuthenticationService
                multiFactorService)
    {
        var result =
            await multiFactorService
                .StartEnrollment(dto);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("mfa/enrollment/confirm")]
    [EnableRateLimiting(
        AuthenticationRateLimitPolicies
            .MultiFactorAuthentication)]
    public async Task<IActionResult>
        ConfirmMfaEnrollment(
            ConfirmMfaEnrollmentDto dto,
            [FromServices]
            IMultiFactorAuthenticationService
                multiFactorService)
    {
        var result =
            await multiFactorService
                .ConfirmEnrollment(dto);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("mfa/recovery-codes/regenerate")]
    [EnableRateLimiting(
        AuthenticationRateLimitPolicies
            .MultiFactorAuthentication)]
    public async Task<IActionResult>
        RegenerateMfaRecoveryCodes(
            RegenerateMfaRecoveryCodesDto dto,
            [FromServices]
            IMultiFactorAuthenticationService
                multiFactorService)
    {
        var result =
            await multiFactorService
                .RegenerateRecoveryCodes(dto);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("mfa/disable")]
    [EnableRateLimiting(
        AuthenticationRateLimitPolicies
            .MultiFactorAuthentication)]
    public async Task<IActionResult>
        DisableMfa(
            DisableMfaDto dto,
            [FromServices]
            IMultiFactorAuthenticationService
                multiFactorService)
    {
        await multiFactorService.Disable(dto);

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
