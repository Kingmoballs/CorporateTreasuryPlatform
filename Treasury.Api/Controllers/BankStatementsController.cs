using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.BankStatements;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/bank-statements")]
[ApiController]
[Authorize(Roles = BankStatementRoles)]
public class BankStatementsController
    : ControllerBase
{
    private const string BankStatementRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly IBankStatementService
        _bankStatementService;

    public BankStatementsController(
        IBankStatementService bankStatementService)
    {
        _bankStatementService =
            bankStatementService;
    }

    [HttpPost("imports")]
    public async Task<IActionResult> ImportStatement(
        CreateBankStatementImportDto dto)
    {
        var result =
            await _bankStatementService
                .ImportStatement(dto);

        return Ok(result);
    }

    [HttpGet("imports/{id}")]
    public async Task<IActionResult> GetImport(
        Guid id)
    {
        var result =
            await _bankStatementService
                .GetImport(id);

        return Ok(result);
    }

    [HttpGet("unmatched")]
    public async Task<IActionResult> GetUnmatchedLines(
        [FromQuery] Guid? accountId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc)
    {
        var result =
            await _bankStatementService
                .GetUnmatchedLines(
                    accountId,
                    fromUtc,
                    toUtc);

        return Ok(result);
    }
}