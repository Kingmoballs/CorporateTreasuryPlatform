using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/investment-rollovers")]
[ApiController]
[Authorize(Roles = RolloverRoles)]
public class InvestmentRolloversController
    : ControllerBase
{
    private const string RolloverRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private const string ApprovalRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly IInvestmentRolloverService
        _quoteService;

    private readonly IInvestmentRolloverRequestService
        _requestService;

    public InvestmentRolloversController(
        IInvestmentRolloverService quoteService,
        IInvestmentRolloverRequestService requestService)
    {
        _quoteService = quoteService;
        _requestService = requestService;
    }

    [HttpGet("{investmentPlacementId:guid}/quote")]
    public async Task<IActionResult> GetQuote(
        Guid investmentPlacementId,
        [FromQuery]
        InvestmentRolloverQuoteRequestDto request)
    {
        var result =
            await _quoteService.GetQuote(
                investmentPlacementId,
                request);

        return Ok(result);
    }

    [HttpPost("{investmentPlacementId:guid}/requests")]
    public async Task<IActionResult> CreateRequest(
        Guid investmentPlacementId,
        CreateInvestmentRolloverRequestDto dto)
    {
        var result =
            await _requestService.Create(
                investmentPlacementId,
                dto);

        return CreatedAtAction(
            nameof(GetRequest),
            new { requestId = result.Id },
            result);
    }

    [HttpGet("requests/{requestId:guid}")]
    public async Task<IActionResult> GetRequest(
        Guid requestId)
    {
        var result =
            await _requestService.GetById(
                requestId);

        return Ok(result);
    }

    [HttpGet("requests/pending")]
    public async Task<IActionResult> GetPending()
    {
        var result =
            await _requestService.GetPending();

        return Ok(result);
    }

    [HttpPost("requests/{requestId:guid}/approve")]
    [Authorize(Roles = ApprovalRoles)]
    public async Task<IActionResult> Approve(
        Guid requestId)
    {
        var result =
            await _requestService.Approve(
                requestId);

        return Ok(result);
    }

    [HttpPost("requests/{requestId:guid}/reject")]
    [Authorize(Roles = ApprovalRoles)]
    public async Task<IActionResult> Reject(
        Guid requestId,
        RejectInvestmentRolloverDto dto)
    {
        var result =
            await _requestService.Reject(
                requestId,
                dto.Reason);

        return Ok(result);
    }
}