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
}