using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IOrganizationRepository
{
    Task<Organization?> GetById(Guid id);

    Task<Organization?> GetByCode(string code);
}
