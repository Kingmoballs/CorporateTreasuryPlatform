using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Counterparties;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[ApiController]
[Route("api/counterparties")]
[Authorize(Roles = ViewerRoles)]
public class CounterpartiesController
    : ControllerBase
{
    private const string ViewerRoles =
        Roles.Admin + "," +
        Roles.CFO + "," +
        Roles.FinanceManager + "," +
        Roles.TreasuryOfficer;

    private const string ManagementRoles =
        Roles.Admin + "," +
        Roles.CFO + "," +
        Roles.FinanceManager;

    private readonly ICounterpartyService
        _counterpartyService;

    public CounterpartiesController(
        ICounterpartyService counterpartyService)
    {
        _counterpartyService =
            counterpartyService;
    }

    [HttpPost]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> Create(
        CreateCounterpartyDto dto)
    {
        var result =
            await _counterpartyService.Create(dto);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                id = result.Id
            },
            result);
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] CounterpartyQueryDto query)
    {
        var result =
            await _counterpartyService.Search(query);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id)
    {
        var result =
            await _counterpartyService
                .GetById(id);

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateCounterpartyDto dto)
    {
        var result =
            await _counterpartyService.Update(
                id,
                dto);

        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> SetStatus(
        Guid id,
        UpdateCounterpartyStatusDto dto)
    {
        var result =
            await _counterpartyService.SetStatus(
                id,
                dto.IsActive);

        return Ok(result);
    }
}