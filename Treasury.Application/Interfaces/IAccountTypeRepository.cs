using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IAccountTypeRepository
{
    Task<AccountType?>
        GetById(Guid id);
}