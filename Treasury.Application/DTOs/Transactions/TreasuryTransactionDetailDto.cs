namespace Treasury.Application.DTOs.Transactions;

public class TreasuryTransactionDetailDto
    : TreasuryTransactionSummaryDto
{
    public Guid? TransferRequestId { get; set; }

    public Guid? InitiatedByUserId { get; set; }

    public Guid? CompletedByUserId { get; set; }

    public Guid? PaymentRequestId { get; set; }

    public Guid? ReversesTransactionId { get; set; }

    public Guid? ReversalRequestId { get; set; }

    public string?  ReversesTransactionReference { get; set; }

    public IReadOnlyList<TransactionLedgerEntryDto>
        LedgerEntries { get; set; }
        = Array.Empty<TransactionLedgerEntryDto>();
}