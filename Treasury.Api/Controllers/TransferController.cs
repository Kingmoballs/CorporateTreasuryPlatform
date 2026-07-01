using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Transfers;
using Treasury.Application.Interfaces;

namespace Treasury.Api.Controllers;

[Route("api/transfers")]
[ApiController]
[Authorize]
public class TransfersController : ControllerBase
{
    private readonly ITransferService _transferService;

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
    public async Task<IActionResult>
        GetPending()
    {
        var result =
            await _transferService
                .GetPendingTransfers();

        return Ok(result);
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult>
        Approve(Guid id)
    {
        var result =
            await _transferService
                .ApproveTransfer(id);

        return Ok(result);
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult>
        Reject(Guid id)
    {
        var result =
            await _transferService
                .RejectTransfer(id);

        return Ok(result);
    }
}