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

    public DbSet<PaymentRequest> PaymentRequests => Set<PaymentRequest>();

    public DbSet<ReversalRequest> ReversalRequests => Set<ReversalRequest>();

    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();

    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

    public DbSet<BankStatementImport> BankStatementImports => Set<BankStatementImport>();

    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();

    private void EnsureFinancialRecordsAreImmutable()
    {
        var changedLedgerEntries =
            ChangeTracker
                .Entries<LedgerEntry>()
                .Where(entry =>
                    entry.State ==
                        EntityState.Modified ||
                    entry.State ==
                        EntityState.Deleted)
                .ToList();

        if (changedLedgerEntries.Count > 0)
        {
            throw new InvalidOperationException(
                "Ledger entries are immutable and " +
                "cannot be modified or deleted.");
        }

        var changedCompletedTransactions =
            ChangeTracker
                .Entries<TreasuryTransaction>()
                .Where(entry =>
                    entry.State ==
                        EntityState.Modified ||
                    entry.State ==
                        EntityState.Deleted)
                .Where(entry =>
                    string.Equals(
                        entry.Property(transaction =>
                                transaction.Status)
                            .OriginalValue,
                        "Completed",
                        StringComparison
                            .OrdinalIgnoreCase))
                .ToList();

        if (changedCompletedTransactions.Count > 0)
        {
            throw new InvalidOperationException(
                "Completed treasury transactions " +
                "are immutable.");
        }

        var changedApprovalDecisions =
            ChangeTracker
                .Entries<ApprovalDecision>()
                .Where(entry =>
                    entry.State ==
                        EntityState.Modified ||
                    entry.State ==
                        EntityState.Deleted)
                .ToList();

        if (changedApprovalDecisions.Count > 0)
        {
            throw new InvalidOperationException(
                "Approval decisions are immutable.");
        }
    }

    public override int SaveChanges()
    {
        EnsureFinancialRecordsAreImmutable();

        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken =
            default)
    {
        EnsureFinancialRecordsAreImmutable();

        return base.SaveChangesAsync(
            cancellationToken);
    }

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
            .HasIndex(request => new
            {
                request.Status,
                request.ExpiresAtUtc
            });

        modelBuilder.Entity<TransferRequest>()
            .HasIndex(request => request.CreatedAt);
        
        modelBuilder.Entity<TransferRequest>()
            .Property(request =>
                request.RequiredApprovalCount)
            .HasDefaultValue(1);

        modelBuilder.Entity<TransferRequest>()
            .Property(request =>
                request.ApprovalCount)
            .HasDefaultValue(0);
        
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
        
        transaction
            .HasIndex(item => item.IdempotencyKey)
            .IsUnique();

        transaction
            .HasIndex(item =>
                item.ExternalReference);

        transaction
            .Property(item => item.IdempotencyKey)
            .HasMaxLength(100);

        transaction
            .Property(item => item.ExternalReference)
            .HasMaxLength(100);

        transaction
            .Property(item => item.Category)
            .HasMaxLength(100);

        transaction
            .Property(item => item.CounterpartyName)
            .HasMaxLength(200);

        modelBuilder.Entity<LedgerEntry>()
            .HasOne(entry =>
                entry.TreasuryTransaction)
            .WithMany(transaction =>
                transaction.LedgerEntries)
            .HasForeignKey(entry =>
                entry.TreasuryTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<Account>()
            .HasIndex(account =>
                account.AccountNumber)
            .IsUnique();

        modelBuilder.Entity<AccountType>()
            .HasIndex(accountType =>
                accountType.Name)
            .IsUnique();

        modelBuilder.Entity<Account>()
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Accounts_Balance_NonNegative",
                    "\"Balance\" >= 0");

                table.HasCheckConstraint(
                    "CK_Accounts_Currency_Length",
                    "char_length(\"Currency\") = 3");

                table.HasCheckConstraint(
                    "CK_Accounts_ReservedBalance_NonNegative",
                    "\"ReservedBalance\" >= 0");

                table.HasCheckConstraint(
                    "CK_Accounts_ReservedBalance_NotAboveBalance",
                    "\"ReservedBalance\" <= \"Balance\"");
            });

        modelBuilder.Entity<LedgerEntry>()
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_LedgerEntries_Amount_Positive",
                    "\"Amount\" > 0");

                table.HasCheckConstraint(
                    "CK_LedgerEntries_EntryType",
                    "\"EntryType\" IN ('Debit', 'Credit')");
            });

        modelBuilder.Entity<TransferRequest>()
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_TransferRequests_Amount_Positive",
                    "\"Amount\" > 0");

                table.HasCheckConstraint(
                    "CK_TransferRequests_Status",
                    "\"Status\" IN " +
                    "('Pending', 'Approved', 'Rejected', 'Expired')");
                
                table.HasCheckConstraint(
                    "CK_TransferRequests_ApprovalCounts",
                    "\"RequiredApprovalCount\" >= 1 " +
                    "AND \"ApprovalCount\" >= 0 " +
                    "AND \"ApprovalCount\" <= " +
                    "\"RequiredApprovalCount\"");
            });

        modelBuilder.Entity<TreasuryTransaction>()
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_TreasuryTransactions_Amount_Positive",
                    "\"Amount\" > 0");

                table.HasCheckConstraint(
                    "CK_TreasuryTransactions_Currency_Length",
                    "char_length(\"Currency\") = 3");

                table.HasCheckConstraint(
                    "CK_TreasuryTransactions_Status",
                    "\"Status\" IN ('Completed')");
            });
        
        var paymentRequest =
            modelBuilder.Entity<PaymentRequest>();

        paymentRequest
            .Property(request =>
                request.ConcurrencyToken)
            .IsConcurrencyToken();

        paymentRequest
            .HasIndex(request =>
                request.IdempotencyKey)
            .IsUnique();

        paymentRequest
            .HasIndex(request =>
                request.Status);
        
        paymentRequest
            .HasIndex(request => new
            {
                request.Status,
                request.ExpiresAtUtc
            });

        paymentRequest
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(request =>
                request.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        paymentRequest
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(request =>
                request.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        paymentRequest
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(request =>
                request.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        paymentRequest
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_PaymentRequests_Amount_Positive",
                    "\"Amount\" > 0");

                table.HasCheckConstraint(
                    "CK_PaymentRequests_Status",
                    "\"Status\" IN " +
                    "('Pending', 'Approved', 'Rejected', 'Expired')");
                
                table.HasCheckConstraint(
                    "CK_PaymentRequests_ApprovalCounts",
                    "\"RequiredApprovalCount\" >= 1 " +
                    "AND \"ApprovalCount\" >= 0 " +
                    "AND \"ApprovalCount\" <= " +
                    "\"RequiredApprovalCount\"");
            });
        
        paymentRequest
            .Property(request =>
                request.RequiredApprovalCount)
            .HasDefaultValue(1);

        paymentRequest
            .Property(request =>
                request.ApprovalCount)
            .HasDefaultValue(0);

        transaction
            .HasOne<PaymentRequest>()
            .WithMany()
            .HasForeignKey(item =>
                item.PaymentRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<Account>()
            .Property(account =>
                account.ConcurrencyToken)
            .IsConcurrencyToken()
            .HasDefaultValueSql(
                "gen_random_uuid()");
        
        var reversalRequest =
            modelBuilder.Entity<ReversalRequest>();

        reversalRequest
            .Property(request =>
                request.ConcurrencyToken)
            .IsConcurrencyToken();

        reversalRequest
            .HasIndex(request =>
                request.OriginalTransactionId)
            .IsUnique();

        reversalRequest
            .HasIndex(request =>
                request.Status);
        
        reversalRequest
            .HasIndex(request => new
            {
                request.Status,
                request.ExpiresAtUtc
            });

        reversalRequest
            .HasOne(request =>
                request.OriginalTransaction)
            .WithMany()
            .HasForeignKey(request =>
                request.OriginalTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        reversalRequest
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(request =>
                request.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        reversalRequest
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(request =>
                request.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        reversalRequest
            .Property(request =>
                request.RequiredApprovalCount)
            .HasDefaultValue(1);

        reversalRequest
            .Property(request =>
                request.ApprovalCount)
            .HasDefaultValue(0);

        reversalRequest
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_ReversalRequests_Status",
                    "\"Status\" IN " +
                    "('Pending', 'Approved', 'Rejected', 'Expired')");
                
                table.HasCheckConstraint(
                    "CK_ReversalRequests_ApprovalCounts",
                    "\"RequiredApprovalCount\" >= 1 " +
                    "AND \"ApprovalCount\" >= 0 " +
                    "AND \"ApprovalCount\" <= " +
                    "\"RequiredApprovalCount\"");
            });

        transaction
            .HasIndex(item =>
                item.ReversesTransactionId)
            .IsUnique();

        transaction
            .HasOne(item =>
                item.ReversesTransaction)
            .WithMany()
            .HasForeignKey(item =>
                item.ReversesTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        transaction
            .HasOne<ReversalRequest>()
            .WithMany()
            .HasForeignKey(item =>
                item.ReversalRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<Account>()
            .Property(account =>
                account.ReservedBalance)
            .HasDefaultValue(0m);
        
        var bankStatementImport =
            modelBuilder.Entity<BankStatementImport>();

        bankStatementImport
            .HasOne(statementImport =>
                statementImport.Account)
            .WithMany()
            .HasForeignKey(statementImport =>
                statementImport.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        bankStatementImport
            .HasOne(statementImport =>
                statementImport.UploadedByUser)
            .WithMany()
            .HasForeignKey(statementImport =>
                statementImport.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        bankStatementImport
            .HasIndex(statementImport => new
            {
                statementImport.AccountId,
                statementImport.StatementReference
            });

        bankStatementImport
            .HasIndex(statementImport =>
                statementImport.UploadedAtUtc);

        bankStatementImport
            .Property(statementImport =>
                statementImport.FileName)
            .HasMaxLength(255);

        bankStatementImport
            .Property(statementImport =>
                statementImport.StatementReference)
            .HasMaxLength(100);

        bankStatementImport
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_BankStatementImports_Currency_Length",
                    "char_length(\"Currency\") = 3");

                table.HasCheckConstraint(
                    "CK_BankStatementImports_LineCount",
                    "\"LineCount\" >= 0");
            });

        var bankStatementLine =
            modelBuilder.Entity<BankStatementLine>();

        bankStatementLine
            .Property(line =>
                line.ConcurrencyToken)
            .IsConcurrencyToken()
            .HasDefaultValueSql(
                "gen_random_uuid()");

        bankStatementLine
            .HasOne(line =>
                line.BankStatementImport)
            .WithMany(statementImport =>
                statementImport.Lines)
            .HasForeignKey(line =>
                line.BankStatementImportId)
            .OnDelete(DeleteBehavior.Cascade);

        bankStatementLine
            .HasOne(line =>
                line.Account)
            .WithMany()
            .HasForeignKey(line =>
                line.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        bankStatementLine
            .HasOne(line =>
                line.MatchedTreasuryTransaction)
            .WithMany()
            .HasForeignKey(line =>
                line.MatchedTreasuryTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        bankStatementLine
            .HasOne(line =>
                line.ReconciledByUser)
            .WithMany()
            .HasForeignKey(line =>
                line.ReconciledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        bankStatementLine
            .HasIndex(line => new
            {
                line.BankStatementImportId,
                line.LineNumber
            })
            .IsUnique();

        bankStatementLine
            .HasIndex(line => new
            {
                line.AccountId,
                line.ReconciliationStatus,
                line.TransactionDateUtc
            });

        bankStatementLine
            .HasIndex(line =>
                line.MatchedTreasuryTransactionId);

        bankStatementLine
            .Property(line =>
                line.Description)
            .HasMaxLength(500);

        bankStatementLine
            .Property(line =>
                line.BankReference)
            .HasMaxLength(100);

        bankStatementLine
            .Property(line =>
                line.CounterpartyName)
            .HasMaxLength(200);

        bankStatementLine
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_BankStatementLines_LineNumber",
                    "\"LineNumber\" > 0");

                table.HasCheckConstraint(
                    "CK_BankStatementLines_Amount_NotZero",
                    "\"Amount\" <> 0");

                table.HasCheckConstraint(
                    "CK_BankStatementLines_Currency_Length",
                    "char_length(\"Currency\") = 3");

                table.HasCheckConstraint(
                    "CK_BankStatementLines_ReconciliationStatus",
                    "\"ReconciliationStatus\" IN " +
                    "('Unmatched', 'Matched', 'Reconciled', 'Ignored')");
            });
        
        var approvalPolicy =
            modelBuilder.Entity<ApprovalPolicy>();

        approvalPolicy
            .HasIndex(policy => new
            {
                policy.OperationType,
                policy.Currency
            })
            .IsUnique();

        approvalPolicy
            .Property(policy =>
                policy.ConcurrencyToken)
            .IsConcurrencyToken()
            .HasDefaultValueSql(
                "gen_random_uuid()");

        approvalPolicy
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(policy =>
                policy.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        approvalPolicy
            .Property(policy =>
                policy.RequiredApprovalCount)
            .HasDefaultValue(1);
        
        approvalPolicy
            .Property(policy =>
                policy.PendingRequestExpiryHours)
            .HasDefaultValue(24);

        approvalPolicy
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_ApprovalPolicies_Threshold",
                    "\"ThresholdAmount\" >= 0");

                table.HasCheckConstraint(
                    "CK_ApprovalPolicies_Currency",
                    "char_length(\"Currency\") = 3");

                table.HasCheckConstraint(
                    "CK_ApprovalPolicies_OperationType",
                    "\"OperationType\" IN " +
                    "('InternalTransfer', " +
                    "'CashPayment', " +
                    "'TransactionReversal')");
                
                table.HasCheckConstraint(
                    "CK_ApprovalPolicies_RequiredApprovalCount",
                    "\"RequiredApprovalCount\" " +
                    "BETWEEN 1 AND 5");
                
                table.HasCheckConstraint(
                    "CK_ApprovalPolicies_PendingRequestExpiryHours",
                    "\"PendingRequestExpiryHours\" " +
                    "BETWEEN 1 AND 168");
            });

            var approvalDecision =
                modelBuilder.Entity<ApprovalDecision>();

            approvalDecision
                .HasIndex(decision => new
                {
                    decision.TransferRequestId,
                    decision.ApproverUserId
                })
                .IsUnique();

            approvalDecision
                .HasIndex(decision => new
                {
                    decision.PaymentRequestId,
                    decision.ApproverUserId
                })
                .IsUnique();

            approvalDecision
                .HasIndex(decision => new
                {
                    decision.ReversalRequestId,
                    decision.ApproverUserId
                })
                .IsUnique();

            approvalDecision
                .HasOne<TransferRequest>()
                .WithMany()
                .HasForeignKey(decision =>
                    decision.TransferRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            approvalDecision
                .HasOne<PaymentRequest>()
                .WithMany()
                .HasForeignKey(decision =>
                    decision.PaymentRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            approvalDecision
                .HasOne<ReversalRequest>()
                .WithMany()
                .HasForeignKey(decision =>
                    decision.ReversalRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            approvalDecision
                .HasOne(decision =>
                    decision.Approver)
                .WithMany()
                .HasForeignKey(decision =>
                    decision.ApproverUserId)
                .OnDelete(DeleteBehavior.Restrict);
            approvalDecision
                .ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_ApprovalDecisions_Decision",
                        "\"Decision\" IN " +
                        "('Approved', 'Rejected')");

                    /*
                    * PostgreSQL num_nonnulls ensures a decision
                    * belongs to exactly one request type.
                    */
                    table.HasCheckConstraint(
                        "CK_ApprovalDecisions_OneRequest",
                        "num_nonnulls(" +
                        "\"TransferRequestId\", " +
                        "\"PaymentRequestId\", " +
                        "\"ReversalRequestId\") = 1");
                });
    }
}