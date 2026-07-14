using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/investment-placements")]
[ApiController]
[Authorize(Roles = InvestmentRoles)]
public class InvestmentPlacementsController
    : ControllerBase
{
    private const string InvestmentRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private const string InvestmentFundingRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly IInvestmentPlacementService
        _placementService;

    public InvestmentPlacementsController(
        IInvestmentPlacementService placementService)
    {
        _placementService =
            placementService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateInvestmentPlacementDto dto)
    {
        var result =
            await _placementService.Create(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] InvestmentPlacementQueryDto query)
    {
        var result =
            await _placementService.Search(query);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id)
    {
        var result =
            await _placementService.GetById(id);

        return Ok(result);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid id,
        ActivateInvestmentPlacementDto dto)
    {
        var result =
            await _placementService.Activate(
                id,
                dto.IdempotencyKey);

        return Ok(result);
    }

    [HttpPost("{id:guid}/approve-activation")]
    [Authorize(Roles = InvestmentFundingRoles)]
    public async Task<IActionResult> ApproveActivation(
        Guid id)
    {
        var result =
            await _placementService
                .ApproveActivation(id);

        return Ok(result);
    }

    [HttpPost("{id:guid}/reject-activation")]
    [Authorize(Roles = InvestmentFundingRoles)]
    public async Task<IActionResult> RejectActivation(
        Guid id,
        RejectInvestmentActivationDto dto)
    {
        var result =
            await _placementService
                .RejectActivation(
                    id,
                    dto.Reason);

        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        CancelInvestmentPlacementDto dto)
    {
        var result =
            await _placementService.Cancel(
                id,
                dto.Reason);

        return Ok(result);
    }
}