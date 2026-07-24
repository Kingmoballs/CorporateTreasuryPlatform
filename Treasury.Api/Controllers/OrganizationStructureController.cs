using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Organizations;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[ApiController]
[Route("api/v1/organization")]
[Authorize(Roles = ViewerRoles)]
public class OrganizationStructureController
    : ControllerBase
{
    private const string ViewerRoles =
        Roles.Admin + "," +
        Roles.CFO + "," +
        Roles.FinanceManager + "," +
        Roles.TreasuryOfficer;

    private const string ManagementRoles =
        Roles.Admin;

    private readonly IOrganizationStructureService
        _service;

    public OrganizationStructureController(
        IOrganizationStructureService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult>
        GetOrganization()
    {
        return Ok(
            await _service.GetOrganization());
    }

    [HttpPut]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult>
        UpdateOrganization(
            UpdateOrganizationProfileDto dto)
    {
        return Ok(
            await _service.UpdateOrganization(dto));
    }

    [HttpGet("legal-entities")]
    public async Task<IActionResult>
        GetLegalEntities()
    {
        return Ok(
            await _service.GetLegalEntities());
    }

    [HttpPost("legal-entities")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult>
        CreateLegalEntity(
            CreateLegalEntityDto dto)
    {
        var result =
            await _service.CreateLegalEntity(dto);

        return CreatedAtAction(
            nameof(GetLegalEntities),
            result);
    }

    [HttpPut("legal-entities/{id:guid}")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult>
        UpdateLegalEntity(
            Guid id,
            UpdateLegalEntityDto dto)
    {
        return Ok(
            await _service.UpdateLegalEntity(
                id,
                dto));
    }

    [HttpPatch(
        "legal-entities/{id:guid}/status")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult>
        SetLegalEntityStatus(
            Guid id,
            UpdateOrganizationStructureStatusDto dto)
    {
        return Ok(
            await _service.SetLegalEntityStatus(
                id,
                dto));
    }

    [HttpGet("business-units")]
    public async Task<IActionResult>
        GetBusinessUnits(
            [FromQuery] Guid? legalEntityId)
    {
        return Ok(
            await _service.GetBusinessUnits(
                legalEntityId));
    }

    [HttpPost("business-units")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult>
        CreateBusinessUnit(
            CreateBusinessUnitDto dto)
    {
        var result =
            await _service.CreateBusinessUnit(dto);

        return CreatedAtAction(
            nameof(GetBusinessUnits),
            result);
    }

    [HttpPut("business-units/{id:guid}")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult>
        UpdateBusinessUnit(
            Guid id,
            UpdateBusinessUnitDto dto)
    {
        return Ok(
            await _service.UpdateBusinessUnit(
                id,
                dto));
    }

    [HttpPatch(
        "business-units/{id:guid}/status")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult>
        SetBusinessUnitStatus(
            Guid id,
            UpdateOrganizationStructureStatusDto dto)
    {
        return Ok(
            await _service.SetBusinessUnitStatus(
                id,
                dto));
    }
}
