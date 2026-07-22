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

    public DbSet<CashFlowForecastItem> CashFlowForecastItems => Set<CashFlowForecastItem>();

    public DbSet<FxRate> FxRates => Set<FxRate>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<TreasuryAlert> TreasuryAlerts => Set<TreasuryAlert>();

    public DbSet<InvestmentPlacement> InvestmentPlacements => Set<InvestmentPlacement>();

    public DbSet<Counterparty> Counterparties =>
        Set<Counterparty>();

    public DbSet<InvestmentLimit> InvestmentLimits =>
        Set<InvestmentLimit>();
    
    public DbSet<CreditFacility> CreditFacilities =>
        Set<CreditFacility>();
    
    public DbSet<CreditFacilityDrawdown>
        CreditFacilityDrawdowns =>
            Set<CreditFacilityDrawdown>();

    public DbSet<InvestmentAccrualSnapshot> 
        InvestmentAccrualSnapshots => 
            Set<InvestmentAccrualSnapshot>();

    public DbSet<InvestmentEarlyRedemptionRequest>
        InvestmentEarlyRedemptionRequests =>
            Set<InvestmentEarlyRedemptionRequest>();

    public DbSet<InvestmentEarlyRedemptionDecision>
        InvestmentEarlyRedemptionDecisions =>
            Set<InvestmentEarlyRedemptionDecision>();
    
    public DbSet<InvestmentRolloverRequest>
        InvestmentRolloverRequests =>
            Set<InvestmentRolloverRequest>();

    public DbSet<InvestmentRolloverDecision>
        InvestmentRolloverDecisions =>
            Set<InvestmentRolloverDecision>();

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

        var changedAuditLogs = ChangeTracker
            .Entries<AuditLog>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (changedAuditLogs)
        {
            throw new InvalidOperationException(
                "Audit logs are immutable and cannot be modified or deleted.");
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

        var changedAccrualSnapshots =
            ChangeTracker
                .Entries<InvestmentAccrualSnapshot>()
                .Any(entry =>
                    entry.State == EntityState.Modified ||
                    entry.State == EntityState.Deleted);

        if (changedAccrualSnapshots)
        {
            throw new InvalidOperationException(
                "Investment accrual snapshots are immutable " +
                "and cannot be modified or deleted.");
        }

        var changedEarlyRedemptionDecisions =
            ChangeTracker
                .Entries<
                    InvestmentEarlyRedemptionDecision>()
                .Any(entry =>
                    entry.State == EntityState.Modified ||
                    entry.State == EntityState.Deleted);

        if (changedEarlyRedemptionDecisions)
        {
            throw new InvalidOperationException(
                "Early-redemption approval decisions " +
                "are immutable.");
        }

        var changedRolloverDecisions =
            ChangeTracker
                .Entries<InvestmentRolloverDecision>()
                .Any(entry =>
                    entry.State == EntityState.Modified ||
                    entry.State == EntityState.Deleted);

        if (changedRolloverDecisions)
        {
            throw new InvalidOperationException(
                "Investment rollover approval decisions " +
                "are immutable.");
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

        var cashFlowForecastItem =
            modelBuilder.Entity<CashFlowForecastItem>();

        cashFlowForecastItem
            .Property(item =>
                item.ConcurrencyToken)
            .IsConcurrencyToken()
            .HasDefaultValueSql(
                "gen_random_uuid()");

        cashFlowForecastItem
            .HasOne(item =>
                item.Account)
            .WithMany()
            .HasForeignKey(item =>
                item.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        cashFlowForecastItem
            .HasOne(item =>
                item.CreatedByUser)
            .WithMany()
            .HasForeignKey(item =>
                item.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        cashFlowForecastItem
            .HasOne(item =>
                item.CancelledByUser)
            .WithMany()
            .HasForeignKey(item =>
                item.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        cashFlowForecastItem
            .HasOne(item =>
                item.RealizedTreasuryTransaction)
            .WithMany()
            .HasForeignKey(item =>
                item.RealizedTreasuryTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        cashFlowForecastItem
            .HasIndex(item => new
            {
                item.Currency,
                item.Status,
                item.ExpectedDateUtc
            });

        cashFlowForecastItem
            .HasIndex(item => new
            {
                item.AccountId,
                item.Status,
                item.ExpectedDateUtc
            });

        cashFlowForecastItem
            .HasIndex(item =>
                item.RealizedTreasuryTransactionId)
            .IsUnique();

        cashFlowForecastItem
            .Property(item =>
                item.Direction)
            .HasMaxLength(20);

        cashFlowForecastItem
            .Property(item =>
                item.Status)
            .HasMaxLength(20);

        cashFlowForecastItem
            .Property(item =>
                item.SourceType)
            .HasMaxLength(50);

        cashFlowForecastItem
            .Property(item =>
                item.Category)
            .HasMaxLength(100);

        cashFlowForecastItem
            .Property(item =>
                item.CounterpartyName)
            .HasMaxLength(200);

        cashFlowForecastItem
            .Property(item =>
                item.Description)
            .HasMaxLength(500);

        cashFlowForecastItem
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_CashFlowForecastItems_Amount_Positive",
                    "\"Amount\" > 0");

                table.HasCheckConstraint(
                    "CK_CashFlowForecastItems_Currency_Length",
                    "char_length(\"Currency\") = 3");

                table.HasCheckConstraint(
                    "CK_CashFlowForecastItems_Direction",
                    "\"Direction\" IN ('Inflow', 'Outflow')");

                table.HasCheckConstraint(
                    "CK_CashFlowForecastItems_Status",
                    "\"Status\" IN ('Active', 'Cancelled', 'Realized')");

                table.HasCheckConstraint(
                    "CK_CashFlowForecastItems_SourceType",
                    "\"SourceType\" IN " +
                    "('Manual', 'CustomerReceipt', 'SupplierPayment', " +
                    "'Payroll', 'Tax', 'Loan', 'Investment', 'Other')");
            });
        
        var fxRate =
            modelBuilder.Entity<FxRate>();

        fxRate
            .Property(rate =>
                rate.ConcurrencyToken)
            .IsConcurrencyToken()
            .HasDefaultValueSql(
                "gen_random_uuid()");

        fxRate
            .HasOne(rate =>
                rate.CreatedByUser)
            .WithMany()
            .HasForeignKey(rate =>
                rate.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        fxRate
            .HasIndex(rate => new
            {
                rate.FromCurrency,
                rate.ToCurrency,
                rate.RateDateUtc
            })
            .IsUnique();

        fxRate
            .HasIndex(rate => new
            {
                rate.ToCurrency,
                rate.RateDateUtc
            });

        fxRate
            .Property(rate =>
                rate.FromCurrency)
            .HasMaxLength(3);

        fxRate
            .Property(rate =>
                rate.ToCurrency)
            .HasMaxLength(3);

        fxRate
            .Property(rate =>
                rate.SourceType)
            .HasMaxLength(50);

        fxRate
            .Property(rate =>
                rate.SourceReference)
            .HasMaxLength(200);

        fxRate
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_FxRates_FromCurrency_Length",
                    "char_length(\"FromCurrency\") = 3");

                table.HasCheckConstraint(
                    "CK_FxRates_ToCurrency_Length",
                    "char_length(\"ToCurrency\") = 3");

                table.HasCheckConstraint(
                    "CK_FxRates_DifferentCurrencies",
                    "\"FromCurrency\" <> \"ToCurrency\"");

                table.HasCheckConstraint(
                    "CK_FxRates_Rate_Positive",
                    "\"Rate\" > 0");

                table.HasCheckConstraint(
                    "CK_FxRates_SourceType",
                    "\"SourceType\" IN " +
                    "('Manual', 'CentralBank', 'Bank', " +
                    "'Market', 'Other')");
            });

        
        var auditLog =
            modelBuilder.Entity<AuditLog>();

        auditLog
            .HasOne(log =>
                log.ActorUser)
            .WithMany()
            .HasForeignKey(log =>
                log.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        auditLog
            .HasIndex(log =>
                log.OccurredAtUtc);

        auditLog
            .HasIndex(log => new
            {
                log.EntityType,
                log.EntityId
            });

        auditLog
            .HasIndex(log => new
            {
                log.ActorUserId,
                log.OccurredAtUtc
            });

        auditLog
            .HasIndex(log =>
                log.Action);

        auditLog
            .Property(log =>
                log.Action)
            .HasMaxLength(50);

        auditLog
            .Property(log =>
                log.EntityType)
            .HasMaxLength(100);

        auditLog
            .Property(log =>
                log.EntityReference)
            .HasMaxLength(200);

        auditLog
            .Property(log =>
                log.Summary)
            .HasMaxLength(500);

        auditLog
            .Property(log =>
                log.ActorEmail)
            .HasMaxLength(200);

        auditLog
            .Property(log =>
                log.ActorRole)
            .HasMaxLength(100);

        auditLog
            .Property(log =>
                log.IpAddress)
            .HasMaxLength(100);

        auditLog
            .Property(log =>
                log.UserAgent)
            .HasMaxLength(500);

        auditLog
            .Property(log =>
                log.BeforeValuesJson)
            .HasColumnType("jsonb");

        auditLog
            .Property(log =>
                log.AfterValuesJson)
            .HasColumnType("jsonb");

        auditLog
            .Property(log =>
                log.MetadataJson)
            .HasColumnType("jsonb");

        auditLog.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_AuditLogs_Action",
                "\"Action\" IN " +
                "('Created','Updated','Deleted','Approved'," +
                "'Rejected','Resolved','Dismissed','Cancelled'," +
                "'Activated','Matured','Redeemed','Realized'," +
                "'Matched','Reconciled','Ignored','Expired'," +
                "'Imported','LoggedIn','RoleChanged'," +
                "'Suspended','Closed','DrawnDown')");

            table.HasCheckConstraint(
                "CK_AuditLogs_EntityType",
                "\"EntityType\" IN " +
                "('User','Role','Account','AccountType'," +
                "'TransferRequest','PaymentRequest'," +
                "'ReversalRequest','ApprovalPolicy'," +
                "'ApprovalDecision','TreasuryTransaction'," +
                "'BankStatementImport','BankStatementLine'," +
                "'CashFlowForecastItem','FxRate'," +
                "'TreasuryAlert','InvestmentPlacement'," +
                "'InvestmentRolloverRequest','Counterparty'," +
                "'InvestmentLimit','CreditFacility'," +
                "'CreditFacilityDrawdown','System')");
        });

        var treasuryAlert =
            modelBuilder.Entity<TreasuryAlert>();

        treasuryAlert
            .Property(alert =>
                alert.ConcurrencyToken)
            .IsConcurrencyToken()
            .HasDefaultValueSql(
                "gen_random_uuid()");

        treasuryAlert
            .HasOne(alert =>
                alert.Account)
            .WithMany()
            .HasForeignKey(alert =>
                alert.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        treasuryAlert
            .HasOne(alert =>
                alert.CreatedByUser)
            .WithMany()
            .HasForeignKey(alert =>
                alert.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        treasuryAlert
            .HasOne(alert =>
                alert.ClosedByUser)
            .WithMany()
            .HasForeignKey(alert =>
                alert.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        treasuryAlert
            .HasIndex(alert => new
            {
                alert.Status,
                alert.Severity
            });

        treasuryAlert
            .HasIndex(alert => new
            {
                alert.AlertType,
                alert.Status
            });

        treasuryAlert
            .HasIndex(alert =>
                alert.CreatedAtUtc);

        treasuryAlert
            .HasIndex(alert =>
                alert.AccountId);

        treasuryAlert
            .HasIndex(alert => new
            {
                alert.SourceEntityType,
                alert.SourceEntityId
            });

        treasuryAlert
            .Property(alert =>
                alert.AlertType)
            .HasMaxLength(100);

        treasuryAlert
            .Property(alert =>
                alert.Severity)
            .HasMaxLength(50);

        treasuryAlert
            .Property(alert =>
                alert.Status)
            .HasMaxLength(50);

        treasuryAlert
            .Property(alert =>
                alert.Title)
            .HasMaxLength(200);

        treasuryAlert
            .Property(alert =>
                alert.Message)
            .HasMaxLength(1000);

        treasuryAlert
            .Property(alert =>
                alert.Currency)
            .HasMaxLength(3);

        treasuryAlert
            .Property(alert =>
                alert.SourceModule)
            .HasMaxLength(100);

        treasuryAlert
            .Property(alert =>
                alert.SourceEntityType)
            .HasMaxLength(100);

        treasuryAlert
            .Property(alert =>
                alert.SourceReference)
            .HasMaxLength(200);

        treasuryAlert
            .Property(alert =>
                alert.ClosureNote)
            .HasMaxLength(500);

        treasuryAlert
            .Property(alert =>
                alert.MetadataJson)
            .HasColumnType("jsonb");

        treasuryAlert
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_TreasuryAlerts_Status",
                    "\"Status\" IN ('Open', 'Resolved', 'Dismissed')");

                table.HasCheckConstraint(
                    "CK_TreasuryAlerts_Severity",
                    "\"Severity\" IN ('Info', 'Warning', 'Critical')");

                table.HasCheckConstraint(
                    "CK_TreasuryAlerts_AlertType",
                    "\"AlertType\" IN " +
                    "('LowLiquidity', 'ForecastLiquidityGap', " +
                    "'PendingApproval', 'ReconciliationException', " +
                    "'FxExposure', 'AuditException', " +
                    "'InvestmentMaturityUpcoming', " +
                    "'InvestmentMaturityOverdue', " +
                    "'InvestmentConcentration', " +
                    "'InvestmentLimitWarning', " +
                    "'InvestmentLimitBreach', 'System')");

                table.HasCheckConstraint(
                    "CK_TreasuryAlerts_Currency_Length",
                    "\"Currency\" IS NULL OR char_length(\"Currency\") = 3");
            });
        
        var counterparty =
            modelBuilder.Entity<Counterparty>();

        counterparty
            .HasIndex(item =>
                item.Code)
            .IsUnique();

        counterparty
            .HasIndex(item => new
            {
                item.IsActive,
                item.Name
            });

        counterparty
            .Property(item =>
                item.Code)
            .HasMaxLength(30);

        counterparty
            .Property(item =>
                item.Name)
            .HasMaxLength(200);

        counterparty
            .Property(item =>
                item.CounterpartyType)
            .HasMaxLength(50);

        counterparty
            .Property(item =>
                item.CountryCode)
            .HasMaxLength(2);

        counterparty
            .Property(item =>
                item.SwiftCode)
            .HasMaxLength(11);

        counterparty
            .Property(item =>
                item.CreditRating)
            .HasMaxLength(20);

        counterparty
            .Property(item =>
                item.Notes)
            .HasMaxLength(1000);

        counterparty
            .Property(item =>
                item.IsActive)
            .HasDefaultValue(true);

        counterparty
            .Property(item =>
                item.ConcurrencyToken)
            .IsConcurrencyToken()
            .HasDefaultValueSql(
                "gen_random_uuid()");

        counterparty
            .HasOne(item =>
                item.CreatedByUser)
            .WithMany()
            .HasForeignKey(item =>
                item.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        counterparty
            .HasOne(item =>
                item.UpdatedByUser)
            .WithMany()
            .HasForeignKey(item =>
                item.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        counterparty
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Counterparties_Code",
                    "\"Code\" ~ " +
                    "'^[A-Z0-9][A-Z0-9-]{0,29}$'");

                table.HasCheckConstraint(
                    "CK_Counterparties_Name",
                    "char_length(btrim(\"Name\")) > 0");

                table.HasCheckConstraint(
                    "CK_Counterparties_Type",
                    "\"CounterpartyType\" IN " +
                    "('Bank'," +
                    "'NonBankFinancialInstitution'," +
                    "'Corporate','Government')");

                table.HasCheckConstraint(
                    "CK_Counterparties_CountryCode",
                    "\"CountryCode\" ~ '^[A-Z]{2}$'");

                table.HasCheckConstraint(
                    "CK_Counterparties_SwiftCode",
                    "\"SwiftCode\" IS NULL OR " +
                    "\"SwiftCode\" ~ " +
                    "'^[A-Z0-9]{8}([A-Z0-9]{3})?$'");
            });

        var investmentLimit =
            modelBuilder.Entity<InvestmentLimit>();

        investmentLimit
            .HasIndex(limit => new
            {
                limit.CounterpartyId,
                limit.Currency,
                limit.InvestmentType,
                limit.EffectiveFromUtc
            })
            .IsUnique();

        investmentLimit
            .HasIndex(limit => new
            {
                limit.IsActive,
                limit.EffectiveFromUtc,
                limit.EffectiveToUtc
            });

        investmentLimit
            .HasOne(limit =>
                limit.Counterparty)
            .WithMany(counterpartyItem =>
                counterpartyItem.InvestmentLimits)
            .HasForeignKey(limit =>
                limit.CounterpartyId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentLimit
            .HasOne(limit =>
                limit.CreatedByUser)
            .WithMany()
            .HasForeignKey(limit =>
                limit.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentLimit
            .HasOne(limit =>
                limit.UpdatedByUser)
            .WithMany()
            .HasForeignKey(limit =>
                limit.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentLimit
            .Property(limit =>
                limit.Currency)
            .HasMaxLength(3);

        investmentLimit
            .Property(limit =>
                limit.InvestmentType)
            .HasMaxLength(50);

        investmentLimit
            .Property(limit =>
                limit.MaximumExposureAmount)
            .HasPrecision(18, 2);

        investmentLimit
            .Property(limit =>
                limit.WarningThresholdPercentage)
            .HasPrecision(5, 2)
            .HasDefaultValue(80m);

        investmentLimit
            .Property(limit =>
                limit.Notes)
            .HasMaxLength(1000);

        investmentLimit
            .Property(limit =>
                limit.IsActive)
            .HasDefaultValue(true);

        investmentLimit
            .Property(limit =>
                limit.ConcurrencyToken)
            .IsConcurrencyToken()
            .HasDefaultValueSql(
                "gen_random_uuid()");

        investmentLimit
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_InvestmentLimits_Currency",
                    "\"Currency\" ~ '^[A-Z]{3}$'");

                table.HasCheckConstraint(
                    "CK_InvestmentLimits_InvestmentType",
                    "\"InvestmentType\" IN " +
                    "('All','FixedDeposit')");

                table.HasCheckConstraint(
                    "CK_InvestmentLimits_MaximumExposure",
                    "\"MaximumExposureAmount\" > 0");

                table.HasCheckConstraint(
                    "CK_InvestmentLimits_WarningThreshold",
                    "\"WarningThresholdPercentage\" > 0 AND " +
                    "\"WarningThresholdPercentage\" <= 100");

                table.HasCheckConstraint(
                    "CK_InvestmentLimits_EffectiveDates",
                    "\"EffectiveToUtc\" IS NULL OR " +
                    "\"EffectiveToUtc\" > \"EffectiveFromUtc\"");
            });
        
        var creditFacility =
            modelBuilder.Entity<CreditFacility>();

        creditFacility
            .HasIndex(facility =>
                facility.Reference)
            .IsUnique();

        creditFacility
            .HasIndex(facility => new
            {
                facility.LenderCounterpartyId,
                facility.Status
            });

        creditFacility
            .HasIndex(facility => new
            {
                facility.Status,
                facility.MaturityDateUtc
            });

        creditFacility
            .HasIndex(facility =>
                facility.SettlementAccountId);

        creditFacility
            .HasIndex(facility =>
                facility.ActivationIdempotencyKey)
            .IsUnique();

        creditFacility
            .Property(facility =>
                facility.Reference)
            .HasMaxLength(50)
            .IsRequired();

        creditFacility
            .Property(facility =>
                facility.FacilityName)
            .HasMaxLength(200)
            .IsRequired();

        creditFacility
            .Property(facility =>
                facility.FacilityType)
            .HasMaxLength(50)
            .IsRequired();

        creditFacility
            .Property(facility =>
                facility.LenderName)
            .HasMaxLength(200)
            .IsRequired();

        creditFacility
            .Property(facility =>
                facility.Currency)
            .HasMaxLength(3)
            .IsRequired();

        creditFacility
            .Property(facility =>
                facility.ApprovedLimitAmount)
            .HasPrecision(18, 2);

        creditFacility
            .Property(facility =>
                facility.OutstandingPrincipalAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        creditFacility
            .Property(facility =>
                facility.AccruedInterestAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        creditFacility
            .Property(facility =>
                facility.AnnualInterestRate)
            .HasPrecision(9, 6);

        creditFacility
            .Property(facility =>
                facility.CommitmentFeeRatePercentage)
            .HasPrecision(9, 6)
            .HasDefaultValue(0m);

        creditFacility
            .Property(facility =>
                facility.ArrangementFeeAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        creditFacility
            .Property(facility =>
                facility.DayCountBasis)
            .HasDefaultValue(365);

        creditFacility
            .Property(facility =>
                facility.InterestPaymentFrequency)
            .HasMaxLength(30)
            .HasDefaultValue("Monthly")
            .IsRequired();

        creditFacility
            .Property(facility =>
                facility.Status)
            .HasMaxLength(50)
            .HasDefaultValue("Draft")
            .IsRequired();

        creditFacility
            .Property(facility =>
                facility.ExternalReference)
            .HasMaxLength(100);

        creditFacility
            .Property(facility =>
                facility.Notes)
            .HasMaxLength(1000);

        creditFacility
            .Property(facility =>
                facility.ActivationIdempotencyKey)
            .HasMaxLength(100);

        creditFacility
            .Property(facility =>
                facility.ActivationRejectionReason)
            .HasMaxLength(500);

        creditFacility
            .Property(facility =>
                facility.SuspensionReason)
            .HasMaxLength(500);

        creditFacility
            .Property(facility =>
                facility.ClosureReason)
            .HasMaxLength(500);

        creditFacility
            .Property(facility =>
                facility.CancellationReason)
            .HasMaxLength(500);

        creditFacility
            .Property(facility =>
                facility.RequiredApprovalCount)
            .HasDefaultValue(0);

        creditFacility
            .Property(facility =>
                facility.ApprovalCount)
            .HasDefaultValue(0);

        creditFacility
            .Property(facility =>
                facility.ConcurrencyToken)
            .IsConcurrencyToken()
            .HasDefaultValueSql(
                "gen_random_uuid()");

        creditFacility
            .HasOne(facility =>
                facility.LenderCounterparty)
            .WithMany(counterparty =>
                counterparty.CreditFacilities)
            .HasForeignKey(facility =>
                facility.LenderCounterpartyId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacility
            .HasOne(facility =>
                facility.SettlementAccount)
            .WithMany()
            .HasForeignKey(facility =>
                facility.SettlementAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacility
            .HasOne(facility =>
                facility.CreatedByUser)
            .WithMany()
            .HasForeignKey(facility =>
                facility.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacility
            .HasOne(facility =>
                facility.UpdatedByUser)
            .WithMany()
            .HasForeignKey(facility =>
                facility.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacility
            .HasOne(facility =>
                facility.ActivationRequestedByUser)
            .WithMany()
            .HasForeignKey(facility =>
                facility.ActivationRequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacility
            .HasOne(facility =>
                facility.ActivationRejectedByUser)
            .WithMany()
            .HasForeignKey(facility =>
                facility.ActivationRejectedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacility
            .HasOne(facility =>
                facility.ActivatedByUser)
            .WithMany()
            .HasForeignKey(facility =>
                facility.ActivatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacility
            .HasOne(facility =>
                facility.SuspendedByUser)
            .WithMany()
            .HasForeignKey(facility =>
                facility.SuspendedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacility
            .HasOne(facility =>
                facility.ClosedByUser)
            .WithMany()
            .HasForeignKey(facility =>
                facility.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacility
            .HasOne(facility =>
                facility.CancelledByUser)
            .WithMany()
            .HasForeignKey(facility =>
                facility.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacility
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_CreditFacilities_ApprovedLimit_Positive",
                    "\"ApprovedLimitAmount\" > 0");

                table.HasCheckConstraint(
                    "CK_CreditFacilities_OutstandingPrincipal_Range",
                    "\"OutstandingPrincipalAmount\" >= 0 AND " +
                    "\"OutstandingPrincipalAmount\" <= " +
                    "\"ApprovedLimitAmount\"");

                table.HasCheckConstraint(
                    "CK_CreditFacilities_AccruedInterest_NonNegative",
                    "\"AccruedInterestAmount\" >= 0");

                table.HasCheckConstraint(
                    "CK_CreditFacilities_InterestRate_Range",
                    "\"AnnualInterestRate\" " +
                    "BETWEEN 0 AND 100");

                table.HasCheckConstraint(
                    "CK_CreditFacilities_CommitmentFeeRate_Range",
                    "\"CommitmentFeeRatePercentage\" " +
                    "BETWEEN 0 AND 100");

                table.HasCheckConstraint(
                    "CK_CreditFacilities_ArrangementFee_NonNegative",
                    "\"ArrangementFeeAmount\" >= 0");

                table.HasCheckConstraint(
                    "CK_CreditFacilities_DayCountBasis",
                    "\"DayCountBasis\" IN (360, 365)");

                table.HasCheckConstraint(
                    "CK_CreditFacilities_Dates",
                    "\"MaturityDateUtc\" > \"StartDateUtc\"");

                table.HasCheckConstraint(
                    "CK_CreditFacilities_Currency",
                    "char_length(\"Currency\") = 3 AND " +
                    "\"Currency\" = upper(\"Currency\")");

                table.HasCheckConstraint(
                    "CK_CreditFacilities_FacilityType",
                    "\"FacilityType\" IN " +
                    "('Overdraft','RevolvingCredit','TermLoan')");

                table.HasCheckConstraint(
                    "CK_CreditFacilities_InterestPaymentFrequency",
                    "\"InterestPaymentFrequency\" IN " +
                    "('Monthly','Quarterly','SemiAnnual'," +
                    "'Annual','AtMaturity')");

                table.HasCheckConstraint(
                    "CK_CreditFacilities_Status",
                    "\"Status\" IN " +
                    "('Draft','PendingActivation','Active'," +
                    "'Suspended','Matured','Closed'," +
                    "'ActivationRejected','ActivationExpired'," +
                    "'Cancelled')");

                table.HasCheckConstraint(
                    "CK_CreditFacilities_ApprovalCounts",
                    "\"RequiredApprovalCount\" BETWEEN 0 AND 5 AND " +
                    "\"ApprovalCount\" BETWEEN 0 AND " +
                    "\"RequiredApprovalCount\"");
            });
        
        var creditFacilityDrawdown =
            modelBuilder.Entity<CreditFacilityDrawdown>();

        creditFacilityDrawdown
            .HasIndex(drawdown =>
                drawdown.Reference)
            .IsUnique();

        creditFacilityDrawdown
            .HasIndex(drawdown =>
                drawdown.IdempotencyKey)
            .IsUnique();

        creditFacilityDrawdown
            .HasIndex(drawdown =>
                drawdown.TreasuryTransactionId)
            .IsUnique();

        creditFacilityDrawdown
            .HasIndex(drawdown => new
            {
                drawdown.CreditFacilityId,
                drawdown.DrawdownDateUtc
            });

        creditFacilityDrawdown
            .HasIndex(drawdown => new
            {
                drawdown.SettlementAccountId,
                drawdown.DrawdownDateUtc
            });

        creditFacilityDrawdown
            .Property(drawdown =>
                drawdown.Reference)
            .HasMaxLength(50)
            .IsRequired();

        creditFacilityDrawdown
            .Property(drawdown =>
                drawdown.Amount)
            .HasPrecision(18, 2);

        creditFacilityDrawdown
            .Property(drawdown =>
                drawdown.Currency)
            .HasMaxLength(3)
            .IsRequired();

        creditFacilityDrawdown
            .Property(drawdown =>
                drawdown.OutstandingPrincipalBefore)
            .HasPrecision(18, 2);

        creditFacilityDrawdown
            .Property(drawdown =>
                drawdown.OutstandingPrincipalAfter)
            .HasPrecision(18, 2);

        creditFacilityDrawdown
            .Property(drawdown =>
                drawdown.Status)
            .HasMaxLength(30)
            .HasDefaultValue("Completed")
            .IsRequired();

        creditFacilityDrawdown
            .Property(drawdown =>
                drawdown.ExternalReference)
            .HasMaxLength(100);

        creditFacilityDrawdown
            .Property(drawdown =>
                drawdown.IdempotencyKey)
            .HasMaxLength(100)
            .IsRequired();

        creditFacilityDrawdown
            .Property(drawdown =>
                drawdown.Description)
            .HasMaxLength(500)
            .IsRequired();

        creditFacilityDrawdown
            .HasOne(drawdown =>
                drawdown.CreditFacility)
            .WithMany(facility =>
                facility.Drawdowns)
            .HasForeignKey(drawdown =>
                drawdown.CreditFacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacilityDrawdown
            .HasOne(drawdown =>
                drawdown.SettlementAccount)
            .WithMany()
            .HasForeignKey(drawdown =>
                drawdown.SettlementAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacilityDrawdown
            .HasOne(drawdown =>
                drawdown.TreasuryTransaction)
            .WithMany()
            .HasForeignKey(drawdown =>
                drawdown.TreasuryTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacilityDrawdown
            .HasOne(drawdown =>
                drawdown.InitiatedByUser)
            .WithMany()
            .HasForeignKey(drawdown =>
                drawdown.InitiatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        creditFacilityDrawdown
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_CreditFacilityDrawdowns_Amount_Positive",
                    "\"Amount\" > 0");

                table.HasCheckConstraint(
                    "CK_CreditFacilityDrawdowns_PrincipalBefore_NonNegative",
                    "\"OutstandingPrincipalBefore\" >= 0");

                /*
                * The post-drawdown principal must equal the
                * previous principal plus this drawdown amount.
                */
                table.HasCheckConstraint(
                    "CK_CreditFacilityDrawdowns_PrincipalMovement",
                    "\"OutstandingPrincipalAfter\" = " +
                    "\"OutstandingPrincipalBefore\" + \"Amount\"");

                table.HasCheckConstraint(
                    "CK_CreditFacilityDrawdowns_Currency",
                    "char_length(\"Currency\") = 3 AND " +
                    "\"Currency\" = upper(\"Currency\")");

                table.HasCheckConstraint(
                    "CK_CreditFacilityDrawdowns_Status",
                    "\"Status\" IN ('Completed')");
            });
        
        var investmentPlacement =
            modelBuilder.Entity<InvestmentPlacement>();

        investmentPlacement
            .HasIndex(placement =>
                placement.Reference)
            .IsUnique();

        investmentPlacement
            .HasIndex(placement => new
            {
                placement.Status,
                placement.MaturityDateUtc
            });

        investmentPlacement
            .HasIndex(placement =>
                placement.SourceAccountId);
        
        investmentPlacement
            .HasIndex(placement =>
                placement.CounterpartyId);

        investmentPlacement
            .HasOne(placement =>
                placement.Counterparty)
            .WithMany(counterpartyItem =>
                counterpartyItem.InvestmentPlacements)
            .HasForeignKey(placement =>
                placement.CounterpartyId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentPlacement
            .Property(placement =>
                placement.ConcurrencyToken)
            .IsConcurrencyToken()
            .HasDefaultValueSql(
                "gen_random_uuid()");

        investmentPlacement
            .HasOne(placement =>
                placement.SourceAccount)
            .WithMany()
            .HasForeignKey(placement =>
                placement.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentPlacement
            .HasOne(placement =>
                placement.CreatedByUser)
            .WithMany()
            .HasForeignKey(placement =>
                placement.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentPlacement
            .HasOne(placement =>
                placement.CancelledByUser)
            .WithMany()
            .HasForeignKey(placement =>
                placement.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentPlacement
            .Property(placement =>
                placement.Reference)
            .HasMaxLength(50);

        investmentPlacement
            .Property(placement =>
                placement.InvestmentType)
            .HasMaxLength(50);

        investmentPlacement
            .Property(placement =>
                placement.InstitutionName)
            .HasMaxLength(200);

        investmentPlacement
            .Property(placement =>
                placement.Currency)
            .HasMaxLength(3);

        investmentPlacement
            .Property(placement =>
                placement.Status)
            .HasMaxLength(50)
            .HasDefaultValue("Draft");

        investmentPlacement
            .Property(placement =>
                placement.ExternalReference)
            .HasMaxLength(100);

        investmentPlacement
            .Property(placement =>
                placement.Notes)
            .HasMaxLength(1000);

        investmentPlacement
            .Property(placement =>
                placement.CancellationReason)
            .HasMaxLength(500);

        investmentPlacement
            .Property(placement =>
                placement.PrincipalAmount)
            .HasPrecision(18, 2);

        investmentPlacement
            .Property(placement =>
                placement.AnnualInterestRate)
            .HasPrecision(9, 6);

        investmentPlacement
            .Property(placement =>
                placement.ExpectedInterestAmount)
            .HasPrecision(18, 2);

        investmentPlacement
            .Property(placement =>
                placement.ExpectedMaturityAmount)
            .HasPrecision(18, 2);
        
        investmentPlacement
            .HasOne(placement =>
                placement.FundingTreasuryTransaction)
            .WithMany()
            .HasForeignKey(placement =>
                placement.FundingTreasuryTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentPlacement
            .HasOne(placement =>
                placement.MaturityForecastItem)
            .WithMany()
            .HasForeignKey(placement =>
                placement.MaturityForecastItemId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentPlacement
            .HasOne(placement =>
                placement.ActivatedByUser)
            .WithMany()
            .HasForeignKey(placement =>
                placement.ActivatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentPlacement
            .HasIndex(placement =>
                placement.ActivationIdempotencyKey)
            .IsUnique();

        investmentPlacement
            .HasIndex(placement =>
                placement.FundingTreasuryTransactionId)
            .IsUnique();

        investmentPlacement
            .HasIndex(placement =>
                placement.MaturityForecastItemId)
            .IsUnique();

        investmentPlacement
            .Property(placement =>
                placement.ActivationIdempotencyKey)
            .HasMaxLength(100);
        
        investmentPlacement
            .HasOne(placement =>
                placement.ActivationRequestedByUser)
            .WithMany()
            .HasForeignKey(placement =>
                placement.ActivationRequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentPlacement
            .HasOne(placement =>
                placement.ActivationRejectedByUser)
            .WithMany()
            .HasForeignKey(placement =>
                placement.ActivationRejectedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentPlacement
            .HasIndex(placement => new
            {
                placement.Status,
                placement.ActivationExpiresAtUtc
            });

        investmentPlacement
            .Property(placement =>
                placement.RequiredApprovalCount)
            .HasDefaultValue(0);

        investmentPlacement
            .Property(placement =>
                placement.ApprovalCount)
            .HasDefaultValue(0);

        investmentPlacement
            .Property(placement =>
                placement.ActivationRejectionReason)
            .HasMaxLength(500);
        
        investmentPlacement
            .HasOne(placement =>
                placement.RedemptionAccount)
            .WithMany()
            .HasForeignKey(placement =>
                placement.RedemptionAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentPlacement
            .HasOne(placement =>
                placement.RedemptionTreasuryTransaction)
            .WithMany()
            .HasForeignKey(placement =>
                placement.RedemptionTreasuryTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentPlacement
            .HasOne(placement =>
                placement.RedeemedByUser)
            .WithMany()
            .HasForeignKey(placement =>
                placement.RedeemedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentPlacement
            .HasIndex(placement =>
                placement.RedemptionIdempotencyKey)
            .IsUnique();

        investmentPlacement
            .HasIndex(placement =>
                placement.RedemptionTreasuryTransactionId)
            .IsUnique();

        investmentPlacement
            .Property(placement =>
                placement.RedemptionIdempotencyKey)
            .HasMaxLength(100);

        investmentPlacement
            .Property(placement =>
                placement.RedemptionExternalReference)
            .HasMaxLength(100);

        investmentPlacement
            .Property(placement =>
                placement.RedemptionNotes)
            .HasMaxLength(1000);

        investmentPlacement
            .Property(placement =>
                placement.ActualInterestAmount)
            .HasPrecision(18, 2);

        investmentPlacement
            .Property(placement =>
                placement.WithholdingTaxAmount)
            .HasPrecision(18, 2);

        investmentPlacement
            .Property(placement =>
                placement.ActualMaturityAmount)
            .HasPrecision(18, 2);

        investmentPlacement
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_InvestmentPlacements_PrincipalAmount_Positive",
                    "\"PrincipalAmount\" > 0");

                table.HasCheckConstraint(
                    "CK_InvestmentPlacements_InterestRate",
                    "\"AnnualInterestRate\" BETWEEN 0 AND 100");

                table.HasCheckConstraint(
                    "CK_InvestmentPlacements_DayCountBasis",
                    "\"DayCountBasis\" IN (360, 365)");

                table.HasCheckConstraint(
                    "CK_InvestmentPlacements_MaturityDate",
                    "\"MaturityDateUtc\" > \"StartDateUtc\"");

                table.HasCheckConstraint(
                    "CK_InvestmentPlacements_Currency_Length",
                    "char_length(\"Currency\") = 3");

                table.HasCheckConstraint(
                    "CK_InvestmentPlacements_InvestmentType",
                    "\"InvestmentType\" IN ('FixedDeposit')");

                table.HasCheckConstraint(
                    "CK_InvestmentPlacements_Status",
                    "\"Status\" IN " +
                    "('Draft', 'PendingActivation', 'Active', " +
                    "'Matured', 'Redeemed', 'ActivationRejected', " +
                    "'ActivationExpired', 'Cancelled')");
                
                table.HasCheckConstraint(
                    "CK_InvestmentPlacements_ApprovalCounts",
                    "\"RequiredApprovalCount\" BETWEEN 0 AND 5 " +
                    "AND \"ApprovalCount\" >= 0 " +
                    "AND \"ApprovalCount\" <= " +
                    "\"RequiredApprovalCount\"");
                
                table.HasCheckConstraint(
                    "CK_InvestmentPlacements_ActualInterest_NonNegative",
                    "\"ActualInterestAmount\" >= 0");

                table.HasCheckConstraint(
                    "CK_InvestmentPlacements_WithholdingTax_NonNegative",
                    "\"WithholdingTaxAmount\" >= 0");

                table.HasCheckConstraint(
                    "CK_InvestmentPlacements_WithholdingTax_NotAboveInterest",
                    "\"WithholdingTaxAmount\" <= " +
                    "\"ActualInterestAmount\"");

                table.HasCheckConstraint(
                    "CK_InvestmentPlacements_RedemptionAmount",
                    "\"Status\" <> 'Redeemed' OR " +
                    "\"ActualMaturityAmount\" > 0");
            });
        
        var investmentAccrualSnapshot =
            modelBuilder.Entity<InvestmentAccrualSnapshot>();

        investmentAccrualSnapshot
            .HasIndex(snapshot => new
            {
                snapshot.InvestmentPlacementId,
                snapshot.SnapshotDateUtc
            })
            .IsUnique();

        investmentAccrualSnapshot
            .HasIndex(snapshot => new
            {
                snapshot.SnapshotDateUtc,
                snapshot.Currency
            });

        investmentAccrualSnapshot
            .HasIndex(snapshot =>
                snapshot.InstitutionName);

        investmentAccrualSnapshot
            .HasOne(snapshot =>
                snapshot.InvestmentPlacement)
            .WithMany()
            .HasForeignKey(snapshot =>
                snapshot.InvestmentPlacementId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentAccrualSnapshot
            .HasOne(snapshot =>
                snapshot.CreatedByUser)
            .WithMany()
            .HasForeignKey(snapshot =>
                snapshot.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.SnapshotDateUtc)
            .HasColumnType("date");

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.InvestmentReference)
            .HasMaxLength(50);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.InstitutionName)
            .HasMaxLength(200);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.Currency)
            .HasMaxLength(3);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.PlacementStatus)
            .HasMaxLength(50);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.PrincipalAmount)
            .HasPrecision(18, 2);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.AnnualInterestRate)
            .HasPrecision(9, 6);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.ExpectedInterestAmount)
            .HasPrecision(18, 2);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.AccruedInterestAmount)
            .HasPrecision(18, 2);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.CarryingAmount)
            .HasPrecision(18, 2);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.ActualInterestAmount)
            .HasPrecision(18, 2);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.WithholdingTaxAmount)
            .HasPrecision(18, 2);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.RealizedNetInterestAmount)
            .HasPrecision(18, 2);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.ActualRedemptionProceeds)
            .HasPrecision(18, 2);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.InterestVarianceAmount)
            .HasPrecision(18, 2);

        investmentAccrualSnapshot
            .Property(snapshot =>
                snapshot.RealizedAnnualizedYieldPercentage)
            .HasPrecision(12, 6);

        investmentAccrualSnapshot
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_InvestmentAccrualSnapshots_Principal",
                    "\"PrincipalAmount\" > 0");

                table.HasCheckConstraint(
                    "CK_InvestmentAccrualSnapshots_Currency",
                    "char_length(\"Currency\") = 3");

                table.HasCheckConstraint(
                    "CK_InvestmentAccrualSnapshots_Rate",
                    "\"AnnualInterestRate\" BETWEEN 0 AND 100");

                table.HasCheckConstraint(
                    "CK_InvestmentAccrualSnapshots_DayCountBasis",
                    "\"DayCountBasis\" IN (360, 365)");

                table.HasCheckConstraint(
                    "CK_InvestmentAccrualSnapshots_AccruedDays",
                    "\"AccruedDays\" >= 0");

                table.HasCheckConstraint(
                    "CK_InvestmentAccrualSnapshots_Amounts",
                    "\"ExpectedInterestAmount\" >= 0 " +
                    "AND \"AccruedInterestAmount\" >= 0 " +
                    "AND \"CarryingAmount\" >= 0 " +
                    "AND \"ActualInterestAmount\" >= 0 " +
                    "AND \"WithholdingTaxAmount\" >= 0 " +
                    "AND \"RealizedNetInterestAmount\" >= 0 " +
                    "AND \"ActualRedemptionProceeds\" >= 0");

                table.HasCheckConstraint(
                    "CK_InvestmentAccrualSnapshots_PositionState",
                    "(\"IsOutstandingAsOf\" AND " +
                    "NOT \"IsRedeemedAsOf\") OR " +
                    "(NOT \"IsOutstandingAsOf\" AND " +
                    "\"IsRedeemedAsOf\")");

                table.HasCheckConstraint(
                    "CK_InvestmentAccrualSnapshots_Status",
                    "\"PlacementStatus\" IN " +
                    "('Active', 'Matured', 'Redeemed')");
            });

        var earlyRedemptionRequest =
            modelBuilder.Entity<
                InvestmentEarlyRedemptionRequest>();

        earlyRedemptionRequest
            .HasIndex(request =>
                request.RequestIdempotencyKey)
            .IsUnique();

        earlyRedemptionRequest
            .HasIndex(request =>
                request.ExecutionIdempotencyKey)
            .IsUnique();

        earlyRedemptionRequest
            .HasIndex(request => new
            {
                request.Status,
                request.ExpiresAtUtc
            });

        earlyRedemptionRequest
            .HasIndex(request =>
                request.InvestmentPlacementId)
            .IsUnique()
            .HasFilter(
                "\"Status\" IN ('Pending', 'Approved')");

        earlyRedemptionRequest
            .Property(request =>
                request.ConcurrencyToken)
            .IsConcurrencyToken()
            .HasDefaultValueSql(
                "gen_random_uuid()");

        earlyRedemptionRequest
            .HasOne(request =>
                request.InvestmentPlacement)
            .WithMany()
            .HasForeignKey(request =>
                request.InvestmentPlacementId)
            .OnDelete(DeleteBehavior.Restrict);

        earlyRedemptionRequest
            .HasOne(request =>
                request.DestinationAccount)
            .WithMany()
            .HasForeignKey(request =>
                request.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        earlyRedemptionRequest
            .HasOne(request =>
                request.RequestedByUser)
            .WithMany()
            .HasForeignKey(request =>
                request.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        earlyRedemptionRequest
            .HasOne(request =>
                request.RejectedByUser)
            .WithMany()
            .HasForeignKey(request =>
                request.RejectedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        earlyRedemptionRequest
            .HasOne(request =>
                request.RedemptionTreasuryTransaction)
            .WithMany()
            .HasForeignKey(request =>
                request.RedemptionTreasuryTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        earlyRedemptionRequest
            .Property(request =>
                request.InvestmentReference)
            .HasMaxLength(50);

        earlyRedemptionRequest
            .Property(request =>
                request.InstitutionName)
            .HasMaxLength(200);

        earlyRedemptionRequest
            .Property(request =>
                request.Currency)
            .HasMaxLength(3);

        earlyRedemptionRequest
            .Property(request =>
                request.Status)
            .HasMaxLength(30);

        earlyRedemptionRequest
            .Property(request =>
                request.RequestIdempotencyKey)
            .HasMaxLength(100);

        earlyRedemptionRequest
            .Property(request =>
                request.ExecutionIdempotencyKey)
            .HasMaxLength(100);

        earlyRedemptionRequest
            .Property(request =>
                request.ExternalReference)
            .HasMaxLength(100);

        earlyRedemptionRequest
            .Property(request =>
                request.Notes)
            .HasMaxLength(1000);

        earlyRedemptionRequest
            .Property(request =>
                request.RejectionReason)
            .HasMaxLength(500);

        earlyRedemptionRequest
            .Property(request =>
                request.PrincipalAmount)
            .HasPrecision(18, 2);

        earlyRedemptionRequest
            .Property(request =>
                request.GrossAccruedInterestAmount)
            .HasPrecision(18, 2);

        earlyRedemptionRequest
            .Property(request =>
                request.PenaltyRatePercentage)
            .HasPrecision(9, 6);

        earlyRedemptionRequest
            .Property(request =>
                request.PenaltyAmount)
            .HasPrecision(18, 2);

        earlyRedemptionRequest
            .Property(request =>
                request.InterestAfterPenaltyAmount)
            .HasPrecision(18, 2);

        earlyRedemptionRequest
            .Property(request =>
                request.WithholdingTaxRatePercentage)
            .HasPrecision(9, 6);

        earlyRedemptionRequest
            .Property(request =>
                request.WithholdingTaxAmount)
            .HasPrecision(18, 2);

        earlyRedemptionRequest
            .Property(request =>
                request.NetInterestAmount)
            .HasPrecision(18, 2);

        earlyRedemptionRequest
            .Property(request =>
                request.EstimatedRedemptionProceeds)
            .HasPrecision(18, 2);

        earlyRedemptionRequest
            .Property(request =>
                request.ExpectedProceedsShortfall)
            .HasPrecision(18, 2);

        earlyRedemptionRequest
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_EarlyRedemptionRequests_Status",
                    "\"Status\" IN " +
                    "('Pending','Approved','Rejected'," +
                    "'Executed','Expired')");

                table.HasCheckConstraint(
                    "CK_EarlyRedemptionRequests_Currency",
                    "char_length(\"Currency\") = 3");

                table.HasCheckConstraint(
                    "CK_EarlyRedemptionRequests_Principal",
                    "\"PrincipalAmount\" > 0");

                table.HasCheckConstraint(
                    "CK_EarlyRedemptionRequests_Rates",
                    "\"PenaltyRatePercentage\" " +
                    "BETWEEN 0 AND 100 AND " +
                    "\"WithholdingTaxRatePercentage\" " +
                    "BETWEEN 0 AND 100");

                table.HasCheckConstraint(
                    "CK_EarlyRedemptionRequests_Amounts",
                    "\"GrossAccruedInterestAmount\" >= 0 " +
                    "AND \"PenaltyAmount\" >= 0 " +
                    "AND \"InterestAfterPenaltyAmount\" >= 0 " +
                    "AND \"WithholdingTaxAmount\" >= 0 " +
                    "AND \"NetInterestAmount\" >= 0 " +
                    "AND \"EstimatedRedemptionProceeds\" > 0 " +
                    "AND \"ExpectedProceedsShortfall\" >= 0");

                table.HasCheckConstraint(
                    "CK_EarlyRedemptionRequests_Approvals",
                    "\"RequiredApprovalCount\" BETWEEN 1 AND 5 " +
                    "AND \"ApprovalCount\" >= 0 " +
                    "AND \"ApprovalCount\" <= " +
                    "\"RequiredApprovalCount\"");

                table.HasCheckConstraint(
                    "CK_EarlyRedemptionRequests_Expiry",
                    "\"ExpiresAtUtc\" > \"RequestedAtUtc\"");
            });

        var earlyRedemptionDecision =
            modelBuilder.Entity<
                InvestmentEarlyRedemptionDecision>();

        earlyRedemptionDecision
            .HasIndex(decision => new
            {
                decision.InvestmentEarlyRedemptionRequestId,
                decision.ApproverUserId
            })
            .IsUnique();

        earlyRedemptionDecision
            .HasOne(decision =>
                decision.InvestmentEarlyRedemptionRequest)
            .WithMany(request =>
                request.Decisions)
            .HasForeignKey(decision =>
                decision.InvestmentEarlyRedemptionRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        earlyRedemptionDecision
            .HasOne(decision =>
                decision.ApproverUser)
            .WithMany()
            .HasForeignKey(decision =>
                decision.ApproverUserId)
            .OnDelete(DeleteBehavior.Restrict);

        earlyRedemptionDecision
            .Property(decision =>
                decision.Decision)
            .HasMaxLength(20);

        earlyRedemptionDecision
            .Property(decision =>
                decision.Comment)
            .HasMaxLength(500);

        earlyRedemptionDecision
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_EarlyRedemptionDecisions_Decision",
                    "\"Decision\" IN ('Approved','Rejected')");
            });

        var rolloverRequest =
            modelBuilder.Entity<InvestmentRolloverRequest>();

        rolloverRequest
            .HasIndex(request =>
                request.RequestIdempotencyKey)
            .IsUnique();

        rolloverRequest
            .HasIndex(request =>
                request.ExecutionIdempotencyKey)
            .IsUnique();

        rolloverRequest
            .HasIndex(request => new
            {
                request.Status,
                request.ExpiresAtUtc
            });

        /*
        * Only one open rollover request is permitted for an
        * original investment at a time.
        */
        rolloverRequest
            .HasIndex(request =>
                request.OriginalInvestmentPlacementId)
            .IsUnique()
            .HasFilter(
                "\"Status\" IN ('Pending', 'Approved')");

        rolloverRequest
            .HasIndex(request =>
                request.NewInvestmentPlacementId)
            .IsUnique();

        rolloverRequest
            .HasIndex(request =>
                request.CashPayoutTreasuryTransactionId)
            .IsUnique();

        rolloverRequest
            .Property(request =>
                request.ConcurrencyToken)
            .IsConcurrencyToken()
            .HasDefaultValueSql(
                "gen_random_uuid()");

        rolloverRequest
            .HasOne(request =>
                request.OriginalInvestmentPlacement)
            .WithMany()
            .HasForeignKey(request =>
                request.OriginalInvestmentPlacementId)
            .OnDelete(DeleteBehavior.Restrict);

        rolloverRequest
            .HasOne(request =>
                request.NewInvestmentPlacement)
            .WithMany()
            .HasForeignKey(request =>
                request.NewInvestmentPlacementId)
            .OnDelete(DeleteBehavior.Restrict);

        rolloverRequest
            .HasOne(request =>
                request.CashPayoutAccount)
            .WithMany()
            .HasForeignKey(request =>
                request.CashPayoutAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        rolloverRequest
            .HasOne(request =>
                request.CashPayoutTreasuryTransaction)
            .WithMany()
            .HasForeignKey(request =>
                request.CashPayoutTreasuryTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        rolloverRequest
            .HasOne(request =>
                request.RequestedByUser)
            .WithMany()
            .HasForeignKey(request =>
                request.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        rolloverRequest
            .HasOne(request =>
                request.RejectedByUser)
            .WithMany()
            .HasForeignKey(request =>
                request.RejectedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        rolloverRequest
            .HasOne(request =>
                request.ExecutedByUser)
            .WithMany()
            .HasForeignKey(request =>
                request.ExecutedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        rolloverRequest
            .Property(request =>
                request.OriginalInvestmentReference)
            .HasMaxLength(50);

        rolloverRequest
            .Property(request =>
                request.OriginalInstitutionName)
            .HasMaxLength(200);

        rolloverRequest
            .Property(request =>
                request.Currency)
            .HasMaxLength(3);

        rolloverRequest
            .Property(request =>
                request.RolloverOption)
            .HasMaxLength(30);

        rolloverRequest
            .Property(request =>
                request.NewInvestmentType)
            .HasMaxLength(50);

        rolloverRequest
            .Property(request =>
                request.NewInstitutionName)
            .HasMaxLength(200);

        rolloverRequest
            .Property(request =>
                request.Status)
            .HasMaxLength(30);

        rolloverRequest
            .Property(request =>
                request.RequestIdempotencyKey)
            .HasMaxLength(100);

        rolloverRequest
            .Property(request =>
                request.ExecutionIdempotencyKey)
            .HasMaxLength(100);

        rolloverRequest
            .Property(request =>
                request.ExternalReference)
            .HasMaxLength(100);

        rolloverRequest
            .Property(request =>
                request.Notes)
            .HasMaxLength(1000);

        rolloverRequest
            .Property(request =>
                request.RejectionReason)
            .HasMaxLength(500);

        rolloverRequest
            .Property(request =>
                request.OriginalPrincipalAmount)
            .HasPrecision(18, 2);

        rolloverRequest
            .Property(request =>
                request.GrossInterestAmount)
            .HasPrecision(18, 2);

        rolloverRequest
            .Property(request =>
                request.GrossMaturityAmount)
            .HasPrecision(18, 2);

        rolloverRequest
            .Property(request =>
                request.WithholdingTaxRatePercentage)
            .HasPrecision(9, 6);

        rolloverRequest
            .Property(request =>
                request.WithholdingTaxAmount)
            .HasPrecision(18, 2);

        rolloverRequest
            .Property(request =>
                request.NetInterestAmount)
            .HasPrecision(18, 2);

        rolloverRequest
            .Property(request =>
                request.NetMaturityProceeds)
            .HasPrecision(18, 2);

        rolloverRequest
            .Property(request =>
                request.RolloverPrincipalAmount)
            .HasPrecision(18, 2);

        rolloverRequest
            .Property(request =>
                request.CashPayoutAmount)
            .HasPrecision(18, 2);

        rolloverRequest
            .Property(request =>
                request.NewAnnualInterestRate)
            .HasPrecision(9, 6);

        rolloverRequest
            .Property(request =>
                request.NewExpectedInterestAmount)
            .HasPrecision(18, 2);

        rolloverRequest
            .Property(request =>
                request.NewExpectedMaturityAmount)
            .HasPrecision(18, 2);

        rolloverRequest
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_InvestmentRolloverRequests_Status",
                    "\"Status\" IN " +
                    "('Pending','Approved','Rejected'," +
                    "'Executed','Expired')");

                table.HasCheckConstraint(
                    "CK_InvestmentRolloverRequests_Currency",
                    "char_length(\"Currency\") = 3");

                table.HasCheckConstraint(
                    "CK_InvestmentRolloverRequests_Option",
                    "\"RolloverOption\" IN " +
                    "('PrincipalOnly'," +
                    "'PrincipalAndNetInterest')");

                table.HasCheckConstraint(
                    "CK_InvestmentRolloverRequests_Rates",
                    "\"WithholdingTaxRatePercentage\" " +
                    "BETWEEN 0 AND 100 AND " +
                    "\"NewAnnualInterestRate\" " +
                    "BETWEEN 0 AND 100");

                table.HasCheckConstraint(
                    "CK_InvestmentRolloverRequests_DayCount",
                    "\"NewDayCountBasis\" IN (360, 365)");

                table.HasCheckConstraint(
                    "CK_InvestmentRolloverRequests_Dates",
                    "\"NewStartDateUtc\" >= " +
                    "\"OriginalMaturityDateUtc\" AND " +
                    "\"NewMaturityDateUtc\" > " +
                    "\"NewStartDateUtc\" AND " +
                    "\"NewTenorDays\" > 0");

                table.HasCheckConstraint(
                    "CK_InvestmentRolloverRequests_Amounts",
                    "\"OriginalPrincipalAmount\" > 0 AND " +
                    "\"GrossInterestAmount\" >= 0 AND " +
                    "\"WithholdingTaxAmount\" >= 0 AND " +
                    "\"WithholdingTaxAmount\" <= " +
                    "\"GrossInterestAmount\" AND " +
                    "\"NetInterestAmount\" >= 0 AND " +
                    "\"NetMaturityProceeds\" > 0 AND " +
                    "\"RolloverPrincipalAmount\" > 0 AND " +
                    "\"CashPayoutAmount\" >= 0 AND " +
                    "\"NewExpectedInterestAmount\" >= 0 AND " +
                    "\"NewExpectedMaturityAmount\" >= " +
                    "\"RolloverPrincipalAmount\"");

                table.HasCheckConstraint(
                    "CK_InvestmentRolloverRequests_Arithmetic",
                    "\"GrossMaturityAmount\" = " +
                    "\"OriginalPrincipalAmount\" + " +
                    "\"GrossInterestAmount\" AND " +
                    "\"NetInterestAmount\" = " +
                    "\"GrossInterestAmount\" - " +
                    "\"WithholdingTaxAmount\" AND " +
                    "\"NetMaturityProceeds\" = " +
                    "\"OriginalPrincipalAmount\" + " +
                    "\"NetInterestAmount\" AND " +
                    "\"NewExpectedMaturityAmount\" = " +
                    "\"RolloverPrincipalAmount\" + " +
                    "\"NewExpectedInterestAmount\"");

                table.HasCheckConstraint(
                    "CK_InvestmentRolloverRequests_OptionAmounts",
                    "(\"RolloverOption\" = 'PrincipalOnly' " +
                    "AND \"RolloverPrincipalAmount\" = " +
                    "\"OriginalPrincipalAmount\" " +
                    "AND \"CashPayoutAmount\" = " +
                    "\"NetInterestAmount\") OR " +
                    "(\"RolloverOption\" = " +
                    "'PrincipalAndNetInterest' " +
                    "AND \"RolloverPrincipalAmount\" = " +
                    "\"NetMaturityProceeds\" " +
                    "AND \"CashPayoutAmount\" = 0)");

                table.HasCheckConstraint(
                    "CK_InvestmentRolloverRequests_PayoutAccount",
                    "(\"CashPayoutAmount\" = 0 AND " +
                    "\"CashPayoutAccountId\" IS NULL) OR " +
                    "(\"CashPayoutAmount\" > 0 AND " +
                    "\"CashPayoutAccountId\" IS NOT NULL)");

                table.HasCheckConstraint(
                    "CK_InvestmentRolloverRequests_Approvals",
                    "\"RequiredApprovalCount\" BETWEEN 1 AND 5 " +
                    "AND \"ApprovalCount\" >= 0 " +
                    "AND \"ApprovalCount\" <= " +
                    "\"RequiredApprovalCount\"");

                table.HasCheckConstraint(
                    "CK_InvestmentRolloverRequests_Expiry",
                    "\"ExpiresAtUtc\" > \"RequestedAtUtc\"");
            });

        var rolloverDecision =
            modelBuilder.Entity<InvestmentRolloverDecision>();

        rolloverDecision
            .HasIndex(decision => new
            {
                decision.InvestmentRolloverRequestId,
                decision.ApproverUserId
            })
            .IsUnique();

        rolloverDecision
            .HasOne(decision =>
                decision.InvestmentRolloverRequest)
            .WithMany(request =>
                request.Decisions)
            .HasForeignKey(decision =>
                decision.InvestmentRolloverRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        rolloverDecision
            .HasOne(decision =>
                decision.ApproverUser)
            .WithMany()
            .HasForeignKey(decision =>
                decision.ApproverUserId)
            .OnDelete(DeleteBehavior.Restrict);

        rolloverDecision
            .Property(decision =>
                decision.Decision)
            .HasMaxLength(20);

        rolloverDecision
            .Property(decision =>
                decision.Comment)
            .HasMaxLength(500);

        rolloverDecision
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_InvestmentRolloverDecisions_Decision",
                    "\"Decision\" IN ('Approved','Rejected')");
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
                    "'TransactionReversal', " +
                    "'InvestmentPlacement', " +
                    "'InvestmentEarlyRedemption', " +
                    "'InvestmentRollover')");
                
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
            .HasIndex(decision => new
            {
                decision.InvestmentPlacementId,
                decision.ApproverUserId
            })
            .IsUnique();

        approvalDecision
            .HasOne<InvestmentPlacement>()
            .WithMany()
            .HasForeignKey(decision =>
                decision.InvestmentPlacementId)
            .OnDelete(DeleteBehavior.Restrict);
        
        approvalDecision
            .HasIndex(decision => new
            {
                decision.CreditFacilityId,
                decision.ApproverUserId
            })
            .IsUnique();

        approvalDecision
            .HasOne<CreditFacility>()
            .WithMany()
            .HasForeignKey(decision =>
                decision.CreditFacilityId)
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
                    "\"ReversalRequestId\", " +
                    "\"InvestmentPlacementId\", " +
                    "\"CreditFacilityId\") = 1");
            });
    }
}