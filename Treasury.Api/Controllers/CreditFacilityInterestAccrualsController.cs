using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.CreditFacilityAccruals;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/credit-facility-interest-accruals")]
[ApiController]
[Authorize(Roles = AccrualReadRoles)]
public class CreditFacilityInterestAccrualsController
    : ControllerBase
{
    private const string AccrualReadRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private const string AccrualProcessingRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly
        ICreditFacilityInterestAccrualService
        _accrualService;

    public CreditFacilityInterestAccrualsController(
        ICreditFacilityInterestAccrualService
            accrualService)
    {
        _accrualService =
            accrualService;
    }

    [HttpPost("generate")]
    [Authorize(Roles = AccrualProcessingRoles)]
    public async Task<IActionResult> Generate(
        GenerateCreditFacilityAccrualsDto dto)
    {
        var result =
            await _accrualService.Generate(dto);

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery]
        CreditFacilityAccrualSnapshotQueryDto query)
    {
        var result =
            await _accrualService.Search(query);

        return Ok(result);
    }
}