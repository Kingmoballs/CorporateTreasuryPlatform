using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.OrganizationOnboarding;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[ApiController]
[Route("api/platform/organization-applications")]
[Authorize(Roles = Roles.PlatformAdmin)]
public class PlatformOrganizationApplicationsController
    : ControllerBase
{
    private readonly IOrganizationOnboardingService
        _service;

    public PlatformOrganizationApplicationsController(
        IOrganizationOnboardingService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery]
            OrganizationApplicationQueryDto query)
    {
        return Ok(
            await _service.Search(query));
    }

    [HttpGet("{applicationId:guid}")]
    public async Task<IActionResult> GetById(
        Guid applicationId)
    {
        return Ok(
            await _service.GetById(
                applicationId));
    }

    [HttpPost("{applicationId:guid}/review")]
    public async Task<IActionResult> BeginReview(
        Guid applicationId,
        ReviewOrganizationApplicationDto dto)
    {
        return Ok(
            await _service.BeginReview(
                applicationId,
                dto));
    }

    [HttpPost("{applicationId:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid applicationId,
        ApproveOrganizationApplicationDto dto)
    {
        return Ok(
            await _service.Approve(
                applicationId,
                dto));
    }

    [HttpPost("{applicationId:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid applicationId,
        RejectOrganizationApplicationDto dto)
    {
        return Ok(
            await _service.Reject(
                applicationId,
                dto));
    }

    [HttpPost(
        "{applicationId:guid}/admin-invitation/resend")]
    public async Task<IActionResult>
        ResendAdminInvitation(Guid applicationId)
    {
        return Ok(
            await _service
                .ResendAdminInvitation(
                    applicationId));
    }
}
