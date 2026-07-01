namespace Treasury.Application.DTOs.Transactions;

public class PagedTreasuryTransactionsDto
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public IReadOnlyList<TreasuryTransactionSummaryDto>
        Items { get; set; }
        = Array.Empty<TreasuryTransactionSummaryDto>();
}