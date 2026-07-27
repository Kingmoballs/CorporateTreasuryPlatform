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
        GetBalanceAggregation(
            [FromQuery] Guid? legalEntityId,
            [FromQuery] Guid? businessUnitId)
    {
        var result =
            await _reportingService
                .GetBalanceAggregation(
                    legalEntityId,
                    businessUnitId);

        return Ok(result);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult>
        GetCashPositionDashboard(
            [FromQuery] Guid? legalEntityId,
            [FromQuery] Guid? businessUnitId)
    {
        var result =
            await _reportingService
                .GetCashPositionDashboard(
                    legalEntityId,
                    businessUnitId);

        return Ok(result);
    }

    [HttpGet("liquidity")]
    public async Task<IActionResult>
        GetLiquidityReport(
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] Guid? legalEntityId,
            [FromQuery] Guid? businessUnitId)
    {
        var result =
            await _reportingService
                .GetLiquidityReport(
                    fromUtc,
                    toUtc,
                    legalEntityId,
                    businessUnitId);

        return Ok(result);
    }

    [HttpGet("liquidity/export/csv")]
    public async Task<IActionResult> ExportLiquidityReportCsv(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? legalEntityId,
        [FromQuery] Guid? businessUnitId)
    {
        var export =
            await _reportingService
                .ExportLiquidityReportCsv(
                    fromUtc,
                    toUtc,
                    legalEntityId,
                    businessUnitId);

        return File(
            export.Content,
            export.ContentType,
            export.FileName);
    }
}
