using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IPaymentRequestRepository
{
    Task Add(PaymentRequest request);

    Task<PaymentRequest?> GetById(Guid id);

    Task<PaymentRequest?>
        GetByIdempotencyKey(string key);

    Task<List<PaymentRequest>> GetPending();

    void Update(PaymentRequest request);

    Task SaveChanges();
}