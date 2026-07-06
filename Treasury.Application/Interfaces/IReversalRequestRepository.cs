using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IReversalRequestRepository
{
    Task Add(ReversalRequest request);

    Task<ReversalRequest?> GetById(Guid id);

    Task<ReversalRequest?>
        GetByOriginalTransactionId(
            Guid transactionId);

    Task<List<ReversalRequest>> GetPending();

    void Update(ReversalRequest request);

    Task SaveChanges();
}