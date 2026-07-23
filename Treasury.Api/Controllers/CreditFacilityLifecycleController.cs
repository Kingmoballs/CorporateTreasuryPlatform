using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.CreditFacilityLifecycle;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/credit-facilities")]
[ApiController]
[Authorize(Roles = LifecycleRoles)]
public class CreditFacilityLifecycleController
    : ControllerBase
{
    private const string LifecycleRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly ICreditFacilityLifecycleService
        _lifecycleService;

    public CreditFacilityLifecycleController(
        ICreditFacilityLifecycleService lifecycleService)
    {
        _lifecycleService =
            lifecycleService;
    }

    [HttpPost("{creditFacilityId:guid}/suspend")]
    public async Task<IActionResult> Suspend(
        Guid creditFacilityId,
        SuspendCreditFacilityDto dto)
    {
        var result =
            await _lifecycleService.Suspend(
                creditFacilityId,
                dto.Reason);

        return Ok(result);
    }

    [HttpPost("{creditFacilityId:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(
        Guid creditFacilityId,
        ReactivateCreditFacilityDto dto)
    {
        var result =
            await _lifecycleService.Reactivate(
                creditFacilityId,
                dto.Reason);

        return Ok(result);
    }

    [HttpPost("{creditFacilityId:guid}/close")]
    public async Task<IActionResult> Close(
        Guid creditFacilityId,
        CloseCreditFacilityDto dto)
    {
        var result =
            await _lifecycleService.Close(
                creditFacilityId,
                dto.Reason);

        return Ok(result);
    }

    [HttpPost("process-maturities")]
    public async Task<IActionResult> ProcessMaturities(
        ProcessCreditFacilityMaturitiesDto dto)
    {
        var result =
            await _lifecycleService
                .ProcessMaturities(dto);

        return Ok(result);
    }
}