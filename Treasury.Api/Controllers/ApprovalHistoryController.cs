using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/approval-history")]
[ApiController]
[Authorize(Roles = ApprovalHistoryRoles)]
public class ApprovalHistoryController
    : ControllerBase
{
    private const string ApprovalHistoryRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly IApprovalHistoryService
        _historyService;

    public ApprovalHistoryController(
        IApprovalHistoryService historyService)
    {
        _historyService = historyService;
    }

    [HttpGet("transfers/{requestId}")]
    public async Task<IActionResult>
        GetTransferHistory(Guid requestId)
    {
        return Ok(
            await _historyService
                .GetTransferHistory(requestId));
    }

    [HttpGet("payments/{requestId}")]
    public async Task<IActionResult>
        GetPaymentHistory(Guid requestId)
    {
        return Ok(
            await _historyService
                .GetPaymentHistory(requestId));
    }

    [HttpGet("reversals/{requestId}")]
    public async Task<IActionResult>
        GetReversalHistory(Guid requestId)
    {
        return Ok(
            await _historyService
                .GetReversalHistory(requestId));
    }
}