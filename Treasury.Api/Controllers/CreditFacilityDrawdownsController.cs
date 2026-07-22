using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.CreditFacilityDrawdowns;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route(
    "api/credit-facilities/{creditFacilityId:guid}/drawdowns")]
[ApiController]
[Authorize(Roles = FacilityRoles)]
public class CreditFacilityDrawdownsController
    : ControllerBase
{
    private const string FacilityRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    /*
     * Drawdown execution immediately changes cash and
     * debt balances, so it is restricted to senior roles.
     */
    private const string DrawdownExecutionRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly ICreditFacilityDrawdownService
        _drawdownService;

    public CreditFacilityDrawdownsController(
        ICreditFacilityDrawdownService drawdownService)
    {
        _drawdownService =
            drawdownService;
    }

    [HttpPost]
    [Authorize(Roles = DrawdownExecutionRoles)]
    public async Task<IActionResult> Execute(
        Guid creditFacilityId,
        CreateCreditFacilityDrawdownDto dto)
    {
        var result =
            await _drawdownService.Execute(
                creditFacilityId,
                dto);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                creditFacilityId,
                drawdownId = result.Id
            },
            result);
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        Guid creditFacilityId,
        [FromQuery] CreditFacilityDrawdownQueryDto query)
    {
        var result =
            await _drawdownService.Search(
                creditFacilityId,
                query);

        return Ok(result);
    }

    [HttpGet("{drawdownId:guid}")]
    public async Task<IActionResult> GetById(
        Guid creditFacilityId,
        Guid drawdownId)
    {
        var result =
            await _drawdownService.GetById(
                drawdownId);

        /*
         * Prevent a drawdown belonging to one facility
         * from being returned beneath another facility URL.
         */
        if (result.CreditFacilityId !=
            creditFacilityId)
        {
            return NotFound();
        }

        return Ok(result);
    }
}