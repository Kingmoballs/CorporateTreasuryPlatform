using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Treasury.Api.Security;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.OrganizationOnboarding;
using Treasury.Application.Interfaces;

namespace Treasury.Api.Controllers;

[ApiController]
[Route("api/v1/organization-applications")]
public class OrganizationApplicationsController
    : ControllerBase
{
    private readonly IOrganizationOnboardingService
        _service;

    public OrganizationApplicationsController(
        IOrganizationOnboardingService service)
    {
        _service = service;
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(
        AuthenticationRateLimitPolicies
            .OrganizationApplication)]
    public async Task<IActionResult> Submit(
        SubmitOrganizationApplicationDto dto,
        [FromHeader(Name = "Idempotency-Key")]
            string? idempotencyKey)
    {
        if (!Guid.TryParse(
                idempotencyKey,
                out var submissionKey) ||
            submissionKey == Guid.Empty)
        {
            throw new RequestValidationException(
                "Idempotency-Key must be a non-empty " +
                "GUID.");
        }

        var result =
            await _service.Submit(
                dto,
                submissionKey);

        return result.IsIdempotentReplay
            ? Ok(result)
            : StatusCode(
                StatusCodes.Status201Created,
                result);
    }
}
