namespace Treasury.Domain.Entities;

public class AccountType
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}