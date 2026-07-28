using Treasury.Application.DTOs.Exports;

namespace Treasury.Application.DTOs.HistoricalImports;

public class CreateHistoricalImportDryRunDto
{
    public Guid ImportKey { get; set; }

    public string Mode { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public byte[] FileContent { get; set; } =
        Array.Empty<byte>();
}

public class HistoricalImportBatchResponseDto
{
    public Guid Id { get; set; }

    public Guid ImportKey { get; set; }

    public string Mode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string FileHash { get; set; } = string.Empty;

    public int TotalRowCount { get; set; }

    public int ValidRowCount { get; set; }

    public int InvalidRowCount { get; set; }

    public Guid UploadedByUserId { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    public DateTime ValidatedAtUtc { get; set; }

    public Guid? SubmittedByUserId { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public int RequiredApprovalCount { get; set; }

    public int ApprovalCount { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public Guid? RejectedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    public Guid? CommittedByUserId { get; set; }

    public DateTime? CommittedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }

    public bool IsIdempotentReplay { get; set; }

    public bool IsPostingOperation { get; set; }

    public string NextAction { get; set; } =
        string.Empty;
}

public class HistoricalImportBatchQueryDto
{
    public string? Mode { get; set; }

    public string? Status { get; set; }

    public Guid? UploadedByUserId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public class PagedHistoricalImportBatchesResponseDto
{
    public IReadOnlyList<HistoricalImportBatchResponseDto>
        Items { get; set; } =
            Array.Empty<
                HistoricalImportBatchResponseDto>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}

public class HistoricalImportDashboardResponseDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public int TotalBatchCount { get; set; }

    public int ValidationFailedCount { get; set; }

    public int ValidatedCount { get; set; }

    public int PendingApprovalCount { get; set; }

    public int ApprovedAwaitingCommitCount
        { get; set; }

    public int RejectedCount { get; set; }

    public int CommittedCount { get; set; }

    public int HistoricalTransactionBatchCount
        { get; set; }

    public int CutoverOpeningBalanceBatchCount
        { get; set; }

    public int HistoricalTransactionRecordCount
        { get; set; }

    public int OpeningBalancePostingCount
        { get; set; }
}

public class HistoricalImportRowResponseDto
{
    public Guid Id { get; set; }

    public int RowNumber { get; set; }

    public string? ExternalReference { get; set; }

    public string AccountNumber { get; set; } =
        string.Empty;

    public Guid? AccountId { get; set; }

    public string? LegalEntityCode { get; set; }

    public Guid? LegalEntityId { get; set; }

    public string? BusinessUnitCode { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public DateTime? TransactionDateUtc { get; set; }

    public DateTime? ValueDateUtc { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public string? Direction { get; set; }

    public string? TransactionType { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public string? CounterpartyName { get; set; }

    public bool IsValid { get; set; }

    public Guid? PostedTreasuryTransactionId
        { get; set; }

    public IReadOnlyList<string> ValidationErrors
        { get; set; } = Array.Empty<string>();
}

public class HistoricalImportRowsQueryDto
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;

    public bool? IsValid { get; set; }
}

public class PagedHistoricalImportRowsResponseDto
{
    public IReadOnlyList<HistoricalImportRowResponseDto>
        Items { get; set; } =
            Array.Empty<HistoricalImportRowResponseDto>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}

public class HistoricalImportTemplateDto
    : CsvExportDto
{
    public string Mode { get; set; } = string.Empty;
}

public class HistoricalImportConcurrencyDto
{
    public Guid ConcurrencyToken { get; set; }
}

public class ReviewHistoricalImportDto
    : HistoricalImportConcurrencyDto
{
    public string? Comment { get; set; }
}

public class RejectHistoricalImportDto
    : HistoricalImportConcurrencyDto
{
    public string Reason { get; set; } =
        string.Empty;
}

public class HistoricalImportDecisionResponseDto
{
    public Guid Id { get; set; }

    public Guid ApproverUserId { get; set; }

    public string ApproverName { get; set; } =
        string.Empty;

    public string ApproverRole { get; set; } =
        string.Empty;

    public string Decision { get; set; } =
        string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public class HistoricalImportApprovalReportResponseDto
{
    public HistoricalImportBatchResponseDto Batch
        { get; set; } = new();

    public IReadOnlyList<
        HistoricalImportDecisionResponseDto>
        Decisions { get; set; } =
            Array.Empty<
                HistoricalImportDecisionResponseDto>();

    public bool HasRequiredApprovals { get; set; }

    public bool HasAdminApproval { get; set; }

    public bool HasFinanceManagerApproval
        { get; set; }

    public bool HasCfoApproval { get; set; }

    public bool HasRejection { get; set; }
}

public class HistoricalImportCommitResponseDto
{
    public HistoricalImportBatchResponseDto Batch
        { get; set; } = new();

    public int HistoricalRecordCount { get; set; }

    public int OpeningBalancePostingCount
        { get; set; }

    public IReadOnlyList<Guid>
        TreasuryTransactionIds { get; set; } =
            Array.Empty<Guid>();
}

public class HistoricalTransactionRecordQueryDto
{
    public Guid? AccountId { get; set; }

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public class HistoricalTransactionRecordResponseDto
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }

    public string ExternalReference { get; set; } =
        string.Empty;

    public Guid AccountId { get; set; }

    public string AccountNumber { get; set; } =
        string.Empty;

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public DateTime TransactionDateUtc { get; set; }

    public DateTime? ValueDateUtc { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } =
        string.Empty;

    public string Direction { get; set; } =
        string.Empty;

    public string TransactionType { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public string? Category { get; set; }

    public string? CounterpartyName { get; set; }

    public DateTime CommittedAtUtc { get; set; }

    public Guid CommittedByUserId { get; set; }
}

public class PagedHistoricalTransactionRecordsResponseDto
{
    public IReadOnlyList<
        HistoricalTransactionRecordResponseDto>
        Items { get; set; } =
            Array.Empty<
                HistoricalTransactionRecordResponseDto>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}

public class OpeningBalanceReconciliationRowResponseDto
{
    public int RowNumber { get; set; }

    public Guid? AccountId { get; set; }

    public string AccountNumber { get; set; } =
        string.Empty;

    public decimal ExpectedOpeningBalance
        { get; set; }

    public string Currency { get; set; } =
        string.Empty;

    public decimal? CurrentAccountBalance
        { get; set; }

    public bool CurrentBalanceMatchesOpening
        { get; set; }

    public Guid? TreasuryTransactionId
        { get; set; }

    public string? TreasuryTransactionReference
        { get; set; }

    public string? TreasuryTransactionStatus
        { get; set; }

    public decimal? TreasuryTransactionAmount
        { get; set; }

    public string? TreasuryTransactionCurrency
        { get; set; }

    public Guid? LedgerEntryId { get; set; }

    public string? LedgerEntryType { get; set; }

    public decimal? LedgerEntryAmount
        { get; set; }

    public bool TransactionMatchesImport
        { get; set; }

    public bool LedgerMatchesImport { get; set; }

    public bool IsPostingReconciled { get; set; }

    public IReadOnlyList<string> Issues
        { get; set; } = Array.Empty<string>();
}

public class OpeningBalanceReconciliationReportResponseDto
{
    public HistoricalImportBatchResponseDto Batch
        { get; set; } = new();

    public int TotalRowCount { get; set; }

    public int ReconciledPostingCount { get; set; }

    public int UnreconciledPostingCount { get; set; }

    public int CurrentBalanceMatchCount { get; set; }

    public int CurrentBalanceDriftCount { get; set; }

    public bool IsFullyPostingReconciled
        { get; set; }

    public IReadOnlyList<
        OpeningBalanceReconciliationRowResponseDto>
        Rows { get; set; } =
            Array.Empty<
                OpeningBalanceReconciliationRowResponseDto>();
}
