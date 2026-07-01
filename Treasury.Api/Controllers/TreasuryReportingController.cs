using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.Interfaces;

namespace Treasury.Api.Controllers;

[Route("api/treasury")]
[ApiController]
[Authorize]
public class TreasuryReportingController
    : ControllerBase
{
    private readonly ITreasuryReportingService
        _reportingService;

    public TreasuryReportingController(
        ITreasuryReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    [HttpGet("balances")]
    public async Task<IActionResult>
        GetBalanceAggregation()
    {
        var result =
            await _reportingService
                .GetBalanceAggregation();

        return Ok(result);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult>
        GetCashPositionDashboard()
    {
        var result =
            await _reportingService
                .GetCashPositionDashboard();

        return Ok(result);
    }

    [HttpGet("liquidity")]
    public async Task<IActionResult>
        GetLiquidityReport(
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc)
    {
        var result =
            await _reportingService
                .GetLiquidityReport(
                    fromUtc,
                    toUtc);

        return Ok(result);
    }
}