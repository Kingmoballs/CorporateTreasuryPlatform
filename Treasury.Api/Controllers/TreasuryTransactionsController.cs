using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Transactions;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/transactions")]
[ApiController]
[Authorize(Roles = TransactionRoles)]
public class TreasuryTransactionsController
    : ControllerBase
{
    private const string TransactionRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly ITreasuryTransactionService
        _transactionService;

    public TreasuryTransactionsController(
        ITreasuryTransactionService
            transactionService)
    {
        _transactionService =
            transactionService;
    }

    [HttpGet]
    public async Task<IActionResult>
        Search(
            [FromQuery] TransactionQueryDto query)
    {
        var result =
            await _transactionService
                .SearchTransactions(query);

        return Ok(result);
    }

    [HttpGet("{reference}")]
    public async Task<IActionResult>
        GetByReference(string reference)
    {
        var result =
            await _transactionService
                .GetByReference(reference);

        return Ok(result);
    }
}