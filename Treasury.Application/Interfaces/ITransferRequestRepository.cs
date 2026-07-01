using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface ITransferRequestRepository
{
    Task Add(TransferRequest request);

    Task<TransferRequest?>
        GetById(Guid id);

    Task<List<TransferRequest>>
        GetPending();

    Task SaveChanges();
}