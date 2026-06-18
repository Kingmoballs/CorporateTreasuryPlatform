using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Accounts;
using Treasury.Application.Interfaces;

namespace Treasury.Api.Controllers;

[Route("api/accounts")]
[ApiController]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(
        IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPost]
    public async Task<IActionResult>
        CreateAccount(
            CreateAccountDto dto)
    {
        var result =
            await _accountService
                .CreateAccount(dto);

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult>
        GetAccounts()
    {
        var result =
            await _accountService
                .GetAccounts();

        return Ok(result);
    }
}