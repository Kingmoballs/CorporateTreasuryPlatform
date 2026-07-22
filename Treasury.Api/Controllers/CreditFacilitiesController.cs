using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.CreditFacilities;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/credit-facilities")]
[ApiController]
[Authorize(Roles = FacilityRoles)]
public class CreditFacilitiesController
    : ControllerBase
{
    private const string FacilityRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private const string ApprovalRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly ICreditFacilityService
        _facilityService;

    public CreditFacilitiesController(
        ICreditFacilityService facilityService)
    {
        _facilityService = facilityService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateCreditFacilityDto dto)
    {
        var result =
            await _facilityService.Create(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] CreditFacilityQueryDto query)
    {
        var result =
            await _facilityService.Search(query);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id)
    {
        var result =
            await _facilityService.GetById(id);

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateCreditFacilityDto dto)
    {
        var result =
            await _facilityService.Update(
                id,
                dto);

        return Ok(result);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid id,
        ActivateCreditFacilityDto dto)
    {
        var result =
            await _facilityService.Activate(
                id,
                dto.IdempotencyKey);

        return Ok(result);
    }

    [HttpPost("{id:guid}/approve-activation")]
    [Authorize(Roles = ApprovalRoles)]
    public async Task<IActionResult> ApproveActivation(
        Guid id)
    {
        var result =
            await _facilityService
                .ApproveActivation(id);

        return Ok(result);
    }

    [HttpPost("{id:guid}/reject-activation")]
    [Authorize(Roles = ApprovalRoles)]
    public async Task<IActionResult> RejectActivation(
        Guid id,
        RejectCreditFacilityActivationDto dto)
    {
        var result =
            await _facilityService
                .RejectActivation(
                    id,
                    dto.Reason);

        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        CancelCreditFacilityDto dto)
    {
        var result =
            await _facilityService.Cancel(
                id,
                dto.Reason);

        return Ok(result);
    }
}