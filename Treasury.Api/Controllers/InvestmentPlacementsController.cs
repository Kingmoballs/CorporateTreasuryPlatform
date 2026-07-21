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

    [HttpPost("process-maturities")]
    [Authorize(Roles = InvestmentFundingRoles)]
    public async Task<IActionResult>
        ProcessMaturities(
            [FromQuery] int maxRows = 100)
    {
        var result =
            await _placementService
                .ProcessDueMaturities(maxRows);

        return Ok(result);
    }

    [HttpGet("portfolio-report")]
    public async Task<IActionResult> GetPortfolioReport(
        [FromQuery] InvestmentPortfolioQueryDto query)
    {
        var result =
            await _placementService
                .GetPortfolioReport(query);

        return Ok(result);
    }

    [HttpGet("maturity-schedule")]
    public async Task<IActionResult> GetMaturitySchedule(
        [FromQuery] InvestmentPortfolioQueryDto query)
    {
        var result =
            await _placementService
                .GetMaturitySchedule(query);

        return Ok(result);
    }

    [HttpGet("portfolio-report/export/csv")]
    public async Task<IActionResult> ExportPortfolioCsv(
        [FromQuery] InvestmentPortfolioQueryDto query)
    {
        var export =
            await _placementService
                .ExportPortfolioCsv(query);

        return File(
            export.Content,
            export.ContentType,
            export.FileName);
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

    [HttpPatch("{id:guid}/counterparty")]
    [Authorize(Roles = InvestmentFundingRoles)]
    public async Task<IActionResult>
        AssignCounterparty(
            Guid id,
            AssignInvestmentCounterpartyDto dto)
    {
        var result =
            await _placementService
                .AssignCounterparty(
                    id,
                    dto.CounterpartyId);

        return Ok(result);
    }

    [HttpPost("{id:guid}/redeem")]
    [Authorize(Roles = InvestmentFundingRoles)]
    public async Task<IActionResult> Redeem(
        Guid id,
        RedeemInvestmentPlacementDto dto)
    {
        var result =
            await _placementService.Redeem(
                id,
                dto);

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