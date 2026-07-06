using Treasury.Application.DTOs.Transactions;

namespace Treasury.Application.DTOs.Reversals;

public class ReversalApprovalResponseDto
{
    public ReversalRequestResponseDto
        Request { get; set; }
        = new();

    public TreasuryTransactionDetailDto?
        Transaction { get; set; }
}