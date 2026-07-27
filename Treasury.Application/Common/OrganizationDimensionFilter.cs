using Treasury.Domain.Entities;

namespace Treasury.Application.Common;

public readonly record struct OrganizationDimensionFilter
{
    private OrganizationDimensionFilter(
        Guid? legalEntityId,
        Guid? businessUnitId)
    {
        LegalEntityId = legalEntityId;
        BusinessUnitId = businessUnitId;
    }

    public Guid? LegalEntityId { get; }

    public Guid? BusinessUnitId { get; }

    public bool IsScoped =>
        LegalEntityId.HasValue ||
        BusinessUnitId.HasValue;

    public static OrganizationDimensionFilter Create(
        Guid? legalEntityId,
        Guid? businessUnitId)
    {
        if (legalEntityId == Guid.Empty)
        {
            throw new ArgumentException(
                "Legal entity ID cannot be empty.",
                nameof(legalEntityId));
        }

        if (businessUnitId == Guid.Empty)
        {
            throw new ArgumentException(
                "Business unit ID cannot be empty.",
                nameof(businessUnitId));
        }

        return new OrganizationDimensionFilter(
            legalEntityId,
            businessUnitId);
    }

    public List<Account> Apply(
        IEnumerable<Account> accounts)
    {
        return accounts
            .Where(Matches)
            .ToList();
    }

    public bool Matches(Account account)
    {
        return
            (!LegalEntityId.HasValue ||
             account.LegalEntityId ==
                LegalEntityId.Value) &&
            (!BusinessUnitId.HasValue ||
             account.BusinessUnitId ==
                BusinessUnitId.Value);
    }
}
