using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Treasury.Domain.Entities;

namespace Treasury.Infrastructure.Persistence;

public class TreasuryDbContext : DbContext
{
    public TreasuryDbContext(
        DbContextOptions<TreasuryDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    public DbSet<Role> Roles { get; set; }

    public DbSet<Account> Accounts  => Set<Account>();

    public DbSet<AccountType> AccountTypes => Set<AccountType>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    
    public DbSet<TransferRequest> TransferRequests => Set<TransferRequest>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId);

        modelBuilder.Entity<Account>()
            .HasOne(x => x.AccountType)
            .WithMany(x => x.Accounts)
            .HasForeignKey(x => x.AccountTypeId);
    }
}