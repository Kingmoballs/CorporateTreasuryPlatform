using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.TreasuryAlerts;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/treasury-alerts")]
[ApiController]
[Authorize(Roles = AlertViewerRoles)]
public class TreasuryAlertsController : ControllerBase
{
    private const string AlertViewerRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private const string AlertManagerRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly ITreasuryAlertService _alertService;
    private readonly ITreasuryAlertMonitoringService _monitoringService;

    public TreasuryAlertsController(
        ITreasuryAlertService alertService,
        ITreasuryAlertMonitoringService monitoringService)
    {
        _alertService =
            alertService;

        _monitoringService =
            monitoringService;
    }

    [HttpPost]
    [Authorize(Roles = AlertManagerRoles)]
    public async Task<IActionResult> Create(
        CreateTreasuryAlertDto dto)
    {
        var result =
            await _alertService.Create(dto);

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] TreasuryAlertQueryDto query)
    {
        var result =
            await _alertService.Search(query);

        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] TreasuryAlertSummaryQueryDto query)
    {
        var result =
            await _alertService.GetSummary(query);

        return Ok(result);
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportAlertsCsv(
        [FromQuery] TreasuryAlertQueryDto query,
        [FromQuery] int maxRows = 5000)
    {
        var result =
            await _alertService.ExportCsv(
                query,
                maxRows);

        return File(
            result.Content,
            result.ContentType,
            result.FileName);
    }

    [HttpPost("{id}/resolve")]
    [Authorize(Roles = AlertManagerRoles)]
    public async Task<IActionResult> Resolve(
        Guid id,
        TreasuryAlertActionDto dto)
    {
        var result =
            await _alertService.Resolve(
                id,
                dto.Note);

        return Ok(result);
    }

    [HttpPost("{id}/dismiss")]
    [Authorize(Roles = AlertManagerRoles)]
    public async Task<IActionResult> Dismiss(
        Guid id,
        TreasuryAlertActionDto dto)
    {
        var result =
            await _alertService.Dismiss(
                id,
                dto.Note);

        return Ok(result);
    }

    [HttpPost("run-scan")]
    [Authorize(Roles = AlertManagerRoles)]
    public async Task<IActionResult> RunScan(
        TreasuryAlertScanRequestDto request)
    {
        var result =
            await _monitoringService.RunScan(request);

        return Ok(result);
    }
}
