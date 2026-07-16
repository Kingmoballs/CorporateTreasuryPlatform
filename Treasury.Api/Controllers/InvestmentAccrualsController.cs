using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/investment-accruals")]
[ApiController]
[Authorize(Roles = InvestmentReportingRoles)]
public class InvestmentAccrualsController
    : ControllerBase
{
    private const string InvestmentReportingRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly IInvestmentAccrualService
        _accrualService;

    public InvestmentAccrualsController(
        IInvestmentAccrualService accrualService)
    {
        _accrualService =
            accrualService;
    }

    [HttpGet("report")]
    public async Task<IActionResult> GetReport(
        [FromQuery] InvestmentAccrualQueryDto query)
    {
        var result =
            await _accrualService.GetReport(query);

        return Ok(result);
    }
}