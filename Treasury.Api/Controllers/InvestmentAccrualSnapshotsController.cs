using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/investment-accrual-snapshots")]
[ApiController]
[Authorize(Roles = SnapshotViewerRoles)]
public class InvestmentAccrualSnapshotsController
    : ControllerBase
{
    private const string SnapshotViewerRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private const string SnapshotGeneratorRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly IInvestmentAccrualSnapshotService
        _snapshotService;

    public InvestmentAccrualSnapshotsController(
        IInvestmentAccrualSnapshotService snapshotService)
    {
        _snapshotService =
            snapshotService;
    }

    [HttpPost("generate")]
    [Authorize(Roles = SnapshotGeneratorRoles)]
    public async Task<IActionResult> Generate(
        GenerateInvestmentAccrualSnapshotsDto dto)
    {
        var result =
            await _snapshotService.Generate(dto);

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] InvestmentAccrualSnapshotQueryDto query)
    {
        var result =
            await _snapshotService.Search(query);

        return Ok(result);
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] InvestmentAccrualSnapshotQueryDto query,
        [FromQuery] int maxRows = 5000)
    {
        var export =
            await _snapshotService.ExportCsv(
                query,
                maxRows);

        return File(
            export.Content,
            export.ContentType,
            export.FileName);
    }
}