using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/investment-rollovers")]
[ApiController]
[Authorize(Roles = RolloverRoles)]
public class InvestmentRolloversController
    : ControllerBase
{
    private const string RolloverRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly IInvestmentRolloverService
        _rolloverService;

    public InvestmentRolloversController(
        IInvestmentRolloverService rolloverService)
    {
        _rolloverService =
            rolloverService;
    }

    [HttpGet("{investmentPlacementId:guid}/quote")]
    public async Task<IActionResult> GetQuote(
        Guid investmentPlacementId,
        [FromQuery]
        InvestmentRolloverQuoteRequestDto request)
    {
        var result =
            await _rolloverService.GetQuote(
                investmentPlacementId,
                request);

        return Ok(result);
    }
}