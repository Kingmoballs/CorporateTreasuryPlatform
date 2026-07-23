using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[ApiController]
[Route(
    "api/v1/admin/authentication-security-events")]
[Authorize(
    Roles =
        Roles.Admin + "," +
        Roles.CFO + "," +
        Roles.FinanceManager)]
public class AuthenticationSecurityEventsController
    : ControllerBase
{
    private readonly IAuthenticationSecurityEventService
        _service;

    public AuthenticationSecurityEventsController(
        IAuthenticationSecurityEventService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? userId,
        [FromQuery] string? eventType,
        [FromQuery] string? outcome,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _service.Search(
            new AuthenticationSecurityEventQueryDto
            {
                UserId = userId,
                EventType = eventType,
                Outcome = outcome,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Page = page,
                PageSize = pageSize
            });

        return Ok(result);
    }
}
