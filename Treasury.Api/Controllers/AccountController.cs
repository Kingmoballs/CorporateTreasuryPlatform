using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Accounts;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/accounts")]
[ApiController]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    private const string AccountManagerRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    public AccountsController(
        IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet("types")]
    public async Task<IActionResult>
        GetAccountTypes()
    {
        var result =
            await _accountService
                .GetAccountTypes();

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = AccountManagerRoles)]
    public async Task<IActionResult>
        CreateAccount(CreateAccountDto dto)
    {
        var result =
            await _accountService
                .CreateAccount(dto);

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult>
        GetAccounts(
            [FromQuery] Guid? legalEntityId,
            [FromQuery] Guid? businessUnitId)
    {
        var result =
            await _accountService
                .GetAccounts(
                    legalEntityId,
                    businessUnitId);

        return Ok(result);
    }

    [HttpGet("{id}/ledger")]
    public async Task<IActionResult>
        GetLedger(Guid id)
    {
        var result =
            await _accountService
                .GetAccountLedger(id);

        return Ok(result);
    }
}
