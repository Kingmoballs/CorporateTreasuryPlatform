using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/investment-early-redemptions")]
[ApiController]
[Authorize(Roles = EarlyRedemptionRoles)]
public class InvestmentEarlyRedemptionsController
    : ControllerBase
{
    private const string EarlyRedemptionRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly IInvestmentEarlyRedemptionService
        _earlyRedemptionService;

    public InvestmentEarlyRedemptionsController(
        IInvestmentEarlyRedemptionService
            earlyRedemptionService)
    {
        _earlyRedemptionService =
            earlyRedemptionService;
    }

    [HttpGet("{investmentPlacementId:guid}/quote")]
    public async Task<IActionResult> GetQuote(
        Guid investmentPlacementId,
        [FromQuery]
        InvestmentEarlyRedemptionQuoteRequestDto request)
    {
        var result =
            await _earlyRedemptionService.GetQuote(
                investmentPlacementId,
                request);

        return Ok(result);
    }
}