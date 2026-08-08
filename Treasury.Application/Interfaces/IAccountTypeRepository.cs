using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IAccountTypeRepository
{
    Task<List<AccountType>>
        GetAll();

    Task<AccountType?>
        GetById(Guid id);
}
