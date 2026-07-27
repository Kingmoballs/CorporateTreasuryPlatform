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
