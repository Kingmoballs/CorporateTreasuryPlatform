namespace Treasury.Domain.Entities;

/*
 * An operating unit, branch or department inside a legal
 * entity. OrganizationId is retained as an explicit tenant
 * key so cross-organization relationships can be rejected.
 */
public class BusinessUnit
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public Organization Organization { get; set; } =
        null!;

    public Guid LegalEntityId { get; set; }

    public LegalEntity LegalEntity { get; set; } =
        null!;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();
}
