using System.ComponentModel.DataAnnotations.Schema;

namespace Treasury.Domain.Entities;


public class Account : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public string Currency { get; set; } = "NGN";

    public bool IsActive { get; set; } = true;

    public Guid AccountTypeId { get; set; }

    public AccountType AccountType { get; set; } = null!;

    public Guid ConcurrencyToken { get; set; }
    = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal ReservedBalance { get; set; }

    [NotMapped]
    public decimal AvailableBalance =>
        Balance - ReservedBalance;
}
