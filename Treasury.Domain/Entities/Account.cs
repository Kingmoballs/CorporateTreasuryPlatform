namespace Treasury.Domain.Entities;

public class Account
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public string Currency { get; set; } = "NGN";

    public bool IsActive { get; set; } = true;

    public Guid AccountTypeId { get; set; }

    public AccountType AccountType { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}