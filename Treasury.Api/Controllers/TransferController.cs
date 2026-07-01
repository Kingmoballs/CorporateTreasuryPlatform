using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Transfers;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/transfers")]
[ApiController]
[Authorize]
public class TransfersController : ControllerBase
{
    private readonly ITransferService _transferService;

    private const string ApproverRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    public TransfersController(
        ITransferService transferService)
    {
        _transferService = transferService;
    }

    [HttpPost]
    public async Task<IActionResult>
        Transfer(CreateTransferDto dto)
    {
        var result =
            await _transferService
                .TransferFunds(dto);

        return Ok(result);
    }

    [HttpGet("pending")]
    [Authorize(Roles = ApproverRoles)]
    public async Task<IActionResult>
        GetPending()
    {
        var result =
            await _transferService
                .GetPendingTransfers();

        return Ok(result);
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = ApproverRoles)]
    public async Task<IActionResult>
        Approve(Guid id)
    {
        var result =
            await _transferService
                .ApproveTransfer(id);

        return Ok(result);
    }

    [HttpPost("{id}/reject")]
    [Authorize(Roles = ApproverRoles)]
    public async Task<IActionResult>
        Reject(
            Guid id,
            RejectTransferDto dto)
    {
        var result =
            await _transferService
                .RejectTransfer(
                    id,
                    dto.Reason);

        return Ok(result);
    }
}