using Treasury.Application.DTOs.CashMovements;

namespace Treasury.Application.Interfaces;

public interface ICashMovementService
{
    Task<CashMovementResponseDto>
        RecordReceipt(
            CreateCashReceiptDto dto);
    
    Task<CashPaymentResponseDto>
        RecordPayment(
            CreateCashPaymentDto dto);

    Task<List<CashPaymentResponseDto>>
        GetPendingPayments();

    Task<CashPaymentResponseDto>
        ApprovePayment(Guid paymentRequestId);

    Task<CashPaymentResponseDto>
        RejectPayment(
            Guid paymentRequestId,
            string reason);
}