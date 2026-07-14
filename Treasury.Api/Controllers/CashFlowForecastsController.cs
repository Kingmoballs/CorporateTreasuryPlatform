using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.CashFlowForecasts;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/cash-flow-forecasts")]
[ApiController]
[Authorize(Roles = ForecastRoles)]
public class CashFlowForecastsController
    : ControllerBase
{
    private const string ForecastRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly ICashFlowForecastService
        _forecastService;

    public CashFlowForecastsController(
        ICashFlowForecastService forecastService)
    {
        _forecastService =
            forecastService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateCashFlowForecastItemDto dto)
    {
        var result =
            await _forecastService.Create(dto);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id)
    {
        var result =
            await _forecastService.GetById(id);

        return Ok(result);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(
        [FromQuery] Guid? accountId,
        [FromQuery] string? currency,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc)
    {
        var result =
            await _forecastService.GetActive(
                accountId,
                currency,
                fromUtc,
                toUtc);

        return Ok(result);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid id)
    {
        var result =
            await _forecastService.Cancel(id);

        return Ok(result);
    }

    [HttpPost("{id}/realize")]
    public async Task<IActionResult> Realize(
        Guid id,
        RealizeCashFlowForecastItemDto dto)
    {
        var result =
            await _forecastService.Realize(
                id,
                dto.TreasuryTransactionId);

        return Ok(result);
    }

    [HttpGet("report")]
    public async Task<IActionResult> GetForecastReport(
        [FromQuery] Guid? accountId,
        [FromQuery] string? currency,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] decimal minimumLiquidityThreshold = 0)
    {
        var result =
            await _forecastService.GetForecastReport(
                accountId,
                currency,
                fromUtc,
                toUtc,
                minimumLiquidityThreshold);

        return Ok(result);
    }

    [HttpGet("variance")]
    public async Task<IActionResult> GetVarianceReport(
        [FromQuery] CashFlowForecastVarianceQueryDto query)
    {
        var result =
            await _forecastService
                .GetVarianceReport(query);

        return Ok(result);
    }

    [HttpGet("variance/export/csv")]
    public async Task<IActionResult> ExportVarianceReportCsv(
        [FromQuery] CashFlowForecastVarianceQueryDto query)
    {
        var export =
            await _forecastService
                .ExportVarianceReportCsv(query);

        return File(
            export.Content,
            export.ContentType,
            export.FileName);
    }
}