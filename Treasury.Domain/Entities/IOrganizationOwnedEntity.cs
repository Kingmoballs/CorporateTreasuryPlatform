namespace Treasury.Domain.Entities;

/*
 * Marks records whose visibility and mutations must be
 * restricted to one organization.
 */
public interface IOrganizationOwnedEntity
{
    Guid OrganizationId { get; set; }
}
