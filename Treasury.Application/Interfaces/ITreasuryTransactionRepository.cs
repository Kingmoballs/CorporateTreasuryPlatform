using Treasury.Application.DTOs.Transactions;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface ITreasuryTransactionRepository
{
    Task Add(
        TreasuryTransaction transaction);

    Task<TreasuryTransaction?>
        GetByReference(string reference);

    Task<(
        IReadOnlyList<TreasuryTransaction> Items,
        int TotalCount)>
        Search(TransactionQueryDto query);
    
    Task<TreasuryTransaction?>
        GetByIdempotencyKey(
            string idempotencyKey);

}