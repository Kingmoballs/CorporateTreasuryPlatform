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
}