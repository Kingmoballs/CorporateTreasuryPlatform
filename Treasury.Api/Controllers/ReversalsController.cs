using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Reversals;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/reversals")]
[ApiController]
[Authorize]
public class ReversalsController : ControllerBase
{
    private const string RequesterRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private const string ApproverRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly IReversalService
        _reversalService;

    public ReversalsController(
        IReversalService reversalService)
    {
        _reversalService = reversalService;
    }

    [HttpPost(
        "/api/transactions/{reference}/reversal-request")]
    [Authorize(Roles = RequesterRoles)]
    public async Task<IActionResult>
        RequestReversal(
            string reference,
            CreateReversalRequestDto dto)
    {
        var result =
            await _reversalService
                .RequestReversal(
                    reference,
                    dto.Reason);

        return Accepted(result);
    }

    [HttpGet("pending")]
    [Authorize(Roles = ApproverRoles)]
    public async Task<IActionResult> GetPending()
    {
        return Ok(
            await _reversalService.GetPending());
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = ApproverRoles)]
    public async Task<IActionResult>
        Approve(Guid id)
    {
        return Ok(
            await _reversalService
                .Approve(id));
    }

    [HttpPost("{id}/reject")]
    [Authorize(Roles = ApproverRoles)]
    public async Task<IActionResult>
        Reject(
            Guid id,
            RejectReversalRequestDto dto)
    {
        return Ok(
            await _reversalService
                .Reject(
                    id,
                    dto.Reason));
    }
}