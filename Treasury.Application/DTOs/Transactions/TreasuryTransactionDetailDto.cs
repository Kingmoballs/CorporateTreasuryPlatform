namespace Treasury.Application.DTOs.Transactions;

public class TreasuryTransactionDetailDto
    : TreasuryTransactionSummaryDto
{
    public Guid? TransferRequestId { get; set; }

    public Guid? InitiatedByUserId { get; set; }

    public Guid? CompletedByUserId { get; set; }

    public IReadOnlyList<TransactionLedgerEntryDto>
        LedgerEntries { get; set; }
        = Array.Empty<TransactionLedgerEntryDto>();
}