using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.CreditFacilityRepayments;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route(
    "api/credit-facilities/{creditFacilityId:guid}/repayments")]
[ApiController]
[Authorize(Roles = FacilityRoles)]
public class CreditFacilityRepaymentsController
    : ControllerBase
{
    private const string FacilityRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private const string RepaymentExecutionRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly ICreditFacilityRepaymentService
        _repaymentService;

    public CreditFacilityRepaymentsController(
        ICreditFacilityRepaymentService repaymentService)
    {
        _repaymentService =
            repaymentService;
    }

    [HttpPost]
    [Authorize(Roles = RepaymentExecutionRoles)]
    public async Task<IActionResult> Execute(
        Guid creditFacilityId,
        CreateCreditFacilityRepaymentDto dto)
    {
        var result =
            await _repaymentService.Execute(
                creditFacilityId,
                dto);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                creditFacilityId,
                repaymentId = result.Id
            },
            result);
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        Guid creditFacilityId,
        [FromQuery] CreditFacilityRepaymentQueryDto query)
    {
        var result =
            await _repaymentService.Search(
                creditFacilityId,
                query);

        return Ok(result);
    }

    [HttpGet("{repaymentId:guid}")]
    public async Task<IActionResult> GetById(
        Guid creditFacilityId,
        Guid repaymentId)
    {
        var result =
            await _repaymentService.GetById(
                repaymentId);

        if (result.CreditFacilityId !=
            creditFacilityId)
        {
            return NotFound();
        }

        return Ok(result);
    }
}