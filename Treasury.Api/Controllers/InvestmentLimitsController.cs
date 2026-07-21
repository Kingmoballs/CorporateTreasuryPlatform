using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.InvestmentLimits;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[ApiController]
[Route("api/investment-limits")]
[Authorize(Roles = ViewerRoles)]
public class InvestmentLimitsController
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

    private readonly IInvestmentLimitService
        _investmentLimitService;
    
    private readonly IInvestmentLimitUtilizationService
        _utilizationService;

    public InvestmentLimitsController(
        IInvestmentLimitService investmentLimitService,
        IInvestmentLimitUtilizationService
            utilizationService)
    {
        _investmentLimitService =
            investmentLimitService;

        _utilizationService =
            utilizationService;
    }

    [HttpPost]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> Create(
        CreateInvestmentLimitDto dto)
    {
        var result =
            await _investmentLimitService
                .Create(dto);

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
        [FromQuery] InvestmentLimitQueryDto query)
    {
        var result =
            await _investmentLimitService
                .Search(query);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id)
    {
        var result =
            await _investmentLimitService
                .GetById(id);

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateInvestmentLimitDto dto)
    {
        var result =
            await _investmentLimitService.Update(
                id,
                dto);

        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> SetStatus(
        Guid id,
        UpdateInvestmentLimitStatusDto dto)
    {
        var result =
            await _investmentLimitService.SetStatus(
                id,
                dto.IsActive);

        return Ok(result);
    }

    [HttpGet("utilization")]
    public async Task<IActionResult> GetUtilization(
        [FromQuery]
        InvestmentLimitUtilizationQueryDto query)
    {
        var result =
            await _utilizationService
                .GetUtilization(query);

        return Ok(result);
    }
}