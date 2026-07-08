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

    [HttpGet("imports/{id}/summary")]
    public async Task<IActionResult> GetReconciliationSummary(
        Guid id)
    {
        var result =
            await _bankStatementService
                .GetReconciliationSummary(id);

        return Ok(result);
    }

    [HttpGet("imports/{id}/exceptions")]
    public async Task<IActionResult> GetExceptionReport(
        Guid id)
    {
        var result =
            await _bankStatementService
                .GetExceptionReport(id);

        return Ok(result);
    }

    [HttpPost("imports/{id}/auto-match")]
    public async Task<IActionResult> AutoMatchImport(
        Guid id,
        [FromQuery] int dateToleranceDays = 2)
    {
        var result =
            await _bankStatementService
                .AutoMatchImport(
                    id,
                    dateToleranceDays);

        return Ok(result);
    }

    [HttpPost("lines/{id}/manual-match")]
    public async Task<IActionResult> ManualMatchLine(
        Guid id,
        ManualBankStatementMatchDto dto)
    {
        var result =
            await _bankStatementService
                .ManualMatchLine(
                    id,
                    dto.TreasuryTransactionId);

        return Ok(result);
    }

    [HttpPost("lines/{id}/reconcile")]
    public async Task<IActionResult> ReconcileLine(
        Guid id)
    {
        var result =
            await _bankStatementService
                .ReconcileLine(id);

        return Ok(result);
    }

    [HttpPost("lines/{id}/unmatch")]
    public async Task<IActionResult> UnmatchLine(
        Guid id)
    {
        var result =
            await _bankStatementService
                .UnmatchLine(id);

        return Ok(result);
    }

    [HttpPost("lines/{id}/ignore")]
    public async Task<IActionResult> IgnoreLine(
        Guid id)
    {
        var result =
            await _bankStatementService
                .IgnoreLine(id);

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