using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.CashMovements;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/cash-movements")]
[ApiController]
[Authorize(Roles = CashMovementRoles)]
public class CashMovementsController
    : ControllerBase
{
    private const string CashMovementRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;
    
    private const string PaymentApproverRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly ICashMovementService
        _cashMovementService;

    public CashMovementsController(
        ICashMovementService cashMovementService)
    {
        _cashMovementService =
            cashMovementService;
    }

    [HttpPost("receipts")]
    public async Task<IActionResult>
        RecordReceipt(
            CreateCashReceiptDto dto)
    {
        var result =
            await _cashMovementService
                .RecordReceipt(dto);

        return Ok(result);
    }

    [HttpPost("payments")]
    public async Task<IActionResult>
        RecordPayment(
            CreateCashPaymentDto dto)
    {
        var result =
            await _cashMovementService
                .RecordPayment(dto);

        if (result.Status ==
            ApprovalStatus.Pending)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    [HttpGet("payments/pending")]
    [Authorize(Roles = PaymentApproverRoles)]
    public async Task<IActionResult>
        GetPendingPayments()
    {
        var result =
            await _cashMovementService
                .GetPendingPayments();

        return Ok(result);
    }

    [HttpPost("payments/{id}/approve")]
    [Authorize(Roles = PaymentApproverRoles)]
    public async Task<IActionResult>
        ApprovePayment(Guid id)
    {
        var result =
            await _cashMovementService
                .ApprovePayment(id);

        return Ok(result);
    }

    [HttpPost("payments/{id}/reject")]
    [Authorize(Roles = PaymentApproverRoles)]
    public async Task<IActionResult>
        RejectPayment(
            Guid id,
            RejectCashPaymentDto dto)
    {
        var result =
            await _cashMovementService
                .RejectPayment(
                    id,
                    dto.Reason);

        return Ok(result);
    }
}