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

    public DbSet<TreasuryTransaction> TreasuryTransactions => Set<TreasuryTransaction>();

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
        
        modelBuilder.Entity<TransferRequest>()
            .Property(request =>
                request.ConcurrencyToken)
            .IsConcurrencyToken();

        modelBuilder.Entity<TransferRequest>()
            .HasIndex(request => request.Status);

        modelBuilder.Entity<TransferRequest>()
            .HasIndex(request => request.CreatedAt);
        
        var transaction =
            modelBuilder.Entity<TreasuryTransaction>();

        transaction
            .HasIndex(item => item.Reference)
            .IsUnique();

        transaction
            .HasIndex(item => item.CreatedAtUtc);

        transaction
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(
                item => item.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        transaction
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(
                item => item.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        transaction
            .HasOne<TransferRequest>()
            .WithMany()
            .HasForeignKey(
                item => item.TransferRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        transaction
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(
                item => item.InitiatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        transaction
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(
                item => item.CompletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LedgerEntry>()
            .HasOne(entry =>
                entry.TreasuryTransaction)
            .WithMany(transaction =>
                transaction.LedgerEntries)
            .HasForeignKey(entry =>
                entry.TreasuryTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}