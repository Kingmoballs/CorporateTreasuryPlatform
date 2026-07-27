using System.Text;
using Treasury.Application.Common;
using Treasury.Application.DTOs.Exports;
using Treasury.Application.DTOs.Reporting;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;


namespace Treasury.Infrastructure.Services;

public class TreasuryReportingService
    : ITreasuryReportingService
{
    private const int DefaultReportDays = 30;

    private const int MaximumReportDays = 366;

    private readonly ILedgerRepository
        _ledgerRepository;

    private readonly IAccountRepository
        _accountRepository;

    private readonly ITransferRequestRepository
        _transferRequestRepository;

    public TreasuryReportingService(
        IAccountRepository accountRepository,
        ITransferRequestRepository
            transferRequestRepository,
        ILedgerRepository ledgerRepository)
    {
        _accountRepository = accountRepository;

        _transferRequestRepository =
            transferRequestRepository;

        _ledgerRepository = ledgerRepository;
    }

    public async Task<BalanceAggregationDto>
        GetBalanceAggregation(
            Guid? legalEntityId = null,
            Guid? businessUnitId = null)
    {
        var filter =
            OrganizationDimensionFilter.Create(
                legalEntityId,
                businessUnitId);

        var accounts =
            filter.Apply(
                await _accountRepository.GetAll());

        var activeAccounts = accounts
            .Where(account => account.IsActive)
            .ToList();

    
        var currencies = activeAccounts
            .GroupBy(
                account =>
                    NormalizeCurrency(
                        account.Currency),
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                group => group.Key,
                StringComparer.OrdinalIgnoreCase)
            .Select(currencyGroup =>
                new CurrencyBalanceSummaryDto
                {
                    Currency = currencyGroup.Key,

                    AccountCount =
                        currencyGroup.Count(),

                    TotalBalance =
                        currencyGroup.Sum(
                            account =>
                                account.Balance),

                    ByAccountType =
                        currencyGroup
                            .GroupBy(
                                account =>
                                    account
                                        .AccountType
                                        .Name,
                                StringComparer
                                    .OrdinalIgnoreCase)
                            .OrderBy(
                                group => group.Key,
                                StringComparer
                                    .OrdinalIgnoreCase)
                            .Select(accountTypeGroup =>
                                new AccountTypeBalanceDto
                                {
                                    AccountType =
                                        accountTypeGroup.Key,

                                    AccountCount =
                                        accountTypeGroup
                                            .Count(),

                                    TotalBalance =
                                        accountTypeGroup
                                            .Sum(
                                                account =>
                                                    account
                                                        .Balance)
                                })
                            .ToList()
                })
            .ToList();

        return new BalanceAggregationDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            LegalEntityId = filter.LegalEntityId,
            BusinessUnitId = filter.BusinessUnitId,
            Currencies = currencies
        };
    }

    public async Task<CashPositionDashboardDto>
        GetCashPositionDashboard(
            Guid? legalEntityId = null,
            Guid? businessUnitId = null)
    {
        var filter =
            OrganizationDimensionFilter.Create(
                legalEntityId,
                businessUnitId);

        var accounts =
            filter.Apply(
                await _accountRepository.GetAll());

        var activeAccounts = accounts
            .Where(account => account.IsActive)
            .ToList();

        var accountsById = accounts
            .ToDictionary(account => account.Id);

        var pendingTransfers =
            (await _transferRequestRepository
                .GetPending())
            .Where(request =>
                accountsById.ContainsKey(
                    request.FromAccountId))
            .ToList();

        /*
         * Pending transfers are grouped using the
         * source account's currency.
         */
        var pendingByCurrency =
            pendingTransfers
                .Where(request =>
                    accountsById.ContainsKey(
                        request.FromAccountId))
                .GroupBy(request =>
                    NormalizeCurrency(
                        accountsById[
                            request.FromAccountId]
                            .Currency))
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(
                        request => request.Amount),
                    StringComparer.OrdinalIgnoreCase);

        var positions = activeAccounts
            .GroupBy(
                account =>
                    NormalizeCurrency(
                        account.Currency),
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                group => group.Key,
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                pendingByCurrency.TryGetValue(
                    group.Key,
                    out var pendingAmount);

                return BuildCashPosition(
                    group.Key,
                    group.ToList(),
                    pendingAmount);
            })
            .ToList();

        return new CashPositionDashboardDto
        {
            GeneratedAtUtc = DateTime.UtcNow,

            LegalEntityId =
                filter.LegalEntityId,

            BusinessUnitId =
                filter.BusinessUnitId,

            ActiveAccountCount =
                activeAccounts.Count,

            PendingApprovalCount =
                pendingTransfers.Count,

            Positions = positions
        };
    }

    private static CurrencyCashPositionDto
        BuildCashPosition(
            string currency,
            List<Account> accounts,
            decimal pendingTransferAmount)
    {
        var balances =
            CalculateLiquidityBalances(accounts);

        return new CurrencyCashPositionDto
        {
            Currency = currency,

            TotalCash =
                balances.TotalCash,

            AvailableLiquidity =
                balances.AvailableLiquidity,

            CommittedCash =
                balances.CommittedCash,

            InvestmentBalance =
                balances.InvestmentBalance,

            OtherBalance =
                balances.OtherBalance,
            
            ReservedCash =
                balances.ReservedCash,

            /*
             * Pending internal transfers are shown
             * separately because they move money
             * between company accounts; they do not
             * reduce company-wide cash.
             */
            PendingInternalTransferAmount =
                pendingTransferAmount,

            AvailableLiquidityRatio =
                CalculateRatio(
                    balances.AvailableLiquidity,
                    balances.TotalCash),

            Accounts = accounts
                .OrderBy(account => account.Name)
                .Select(account =>
                    new CashPositionAccountDto
                    {
                        AccountId =
                            account.Id,

                        Name =
                            account.Name,

                        AccountNumber =
                            account.AccountNumber,

                        AccountType =
                            account.AccountType.Name,

                        LegalEntityId =
                            account.LegalEntityId,

                        LegalEntityCode =
                            account.LegalEntity?.Code,

                        BusinessUnitId =
                            account.BusinessUnitId,

                        BusinessUnitCode =
                            account.BusinessUnit?.Code,

                        Balance =
                            account.Balance,

                        ReservedBalance =
                            account.ReservedBalance,

                        AvailableBalance =
                            account.AvailableBalance
                    })
                .ToList()
        };
    }

    private static LiquidityBalances
        CalculateLiquidityBalances(
            IEnumerable<Account> accounts)
    {
        decimal totalCash = 0;
        decimal availableLiquidity = 0;
        decimal committedCash = 0;
        decimal investmentBalance = 0;
        decimal otherBalance = 0;
        decimal reservedCash = 0;

        foreach (var account in accounts)
        {
            totalCash += account.Balance;
            reservedCash += account.ReservedBalance;

            /*
             * These classifications represent the
             * current treasury liquidity policy.
             */
            if (IsAccountType(
                account,
                AccountTypes.Operating,
                AccountTypes.Reserve))
            {
                availableLiquidity +=
                    account.AvailableBalance;
            }
            else if (IsAccountType(
                account,
                AccountTypes.Payroll,
                AccountTypes.Tax))
            {
                committedCash +=
                    account.Balance;
            }
            else if (IsAccountType(
                account,
                AccountTypes.Investment))
            {
                investmentBalance +=
                    account.Balance;
            }
            else
            {
                otherBalance +=
                    account.Balance;
            }
        }

        return new LiquidityBalances(
            totalCash,
            availableLiquidity,
            committedCash,
            investmentBalance,
            otherBalance,
            reservedCash);
    }

    private static bool IsAccountType(
        Account account,
        params string[] accountTypes)
    {
        return accountTypes.Contains(
            account.AccountType.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    private static decimal CalculateRatio(
        decimal availableLiquidity,
        decimal totalCash)
    {
        if (totalCash <= 0)
        {
            return 0;
        }

        return Math.Round(
            availableLiquidity
                / totalCash
                * 100,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static string NormalizeCurrency(
        string currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "UNKNOWN"
            : currency.Trim().ToUpperInvariant();
    }

    private static bool CurrencyMatches(
        string accountCurrency,
        string requestedCurrency)
    {
        return string.Equals(
            NormalizeCurrency(accountCurrency),
            requestedCurrency,
            StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime NormalizeDateTime(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc =>
                value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc)
        };
    }

    private static void ValidateReportPeriod(
        DateTime fromUtc,
        DateTime toUtc)
    {
        if (fromUtc >= toUtc)
        {
            throw new ArgumentException(
                "The report start must be earlier " +
                "than the report end.");
        }

        if ((toUtc - fromUtc).TotalDays >
            MaximumReportDays)
        {
            throw new ArgumentException(
                $"The report period cannot exceed " +
                $"{MaximumReportDays} days.");
        }
    }

    private sealed record LiquidityBalances(
        decimal TotalCash,
        decimal AvailableLiquidity,
        decimal CommittedCash,
        decimal InvestmentBalance,
        decimal OtherBalance,
        decimal ReservedCash);
    
    public async Task<LiquidityReportDto>
        GetLiquidityReport(
            DateTime? fromUtc,
            DateTime? toUtc,
            Guid? legalEntityId = null,
            Guid? businessUnitId = null)
    {
        var filter =
            OrganizationDimensionFilter.Create(
                legalEntityId,
                businessUnitId);

        var reportToUtc =
            NormalizeDateTime(
                toUtc ?? DateTime.UtcNow);

        var reportFromUtc =
            NormalizeDateTime(
                fromUtc ??
                reportToUtc.AddDays(
                    -DefaultReportDays));

        ValidateReportPeriod(
            reportFromUtc,
            reportToUtc);

        var accounts =
            filter.Apply(
                await _accountRepository.GetAll());

        var scopedAccountIds =
            accounts
                .Select(account => account.Id)
                .ToHashSet();

        var ledgerEntries =
            (await _ledgerRepository
                .GetByDateRange(
                    reportFromUtc,
                    reportToUtc))
            .Where(entry =>
                scopedAccountIds.Contains(
                    entry.AccountId))
            .ToList();

        var pendingTransfers =
            (await _transferRequestRepository
                .GetPending())
            .Where(request =>
                scopedAccountIds.Contains(
                    request.FromAccountId))
            .ToList();

        var activeAccounts = accounts
            .Where(account => account.IsActive)
            .ToList();

        var accountsById = accounts
            .ToDictionary(account => account.Id);

        /*
        * A completed internal transfer creates one
        * source Credit and one destination Debit.
        * Counting only Credit entries prevents the
        * transfer volume from being doubled.
        */
        var completedTransfers = ledgerEntries
            .Where(entry =>
                entry.TreasuryTransaction?
                    .TransactionType ==
                        TransactionTypes.InternalTransfer &&
                string.Equals(
                    entry.EntryType,
                    "Credit",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        var cashReceipts = ledgerEntries
            .Where(entry =>
                entry.TreasuryTransaction?
                    .TransactionType ==
                        TransactionTypes.CashReceipt &&
                string.Equals(
                    entry.EntryType,
                    "Debit",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        var cashPayments = ledgerEntries
            .Where(entry =>
                entry.TreasuryTransaction?
                    .TransactionType ==
                        TransactionTypes.CashPayment &&
                string.Equals(
                    entry.EntryType,
                    "Credit",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        var currencies = activeAccounts
            .Select(account =>
                NormalizeCurrency(
                    account.Currency))
            .Concat(
                completedTransfers.Select(entry =>
                    NormalizeCurrency(
                        entry.Account.Currency)))
            .Concat(
                pendingTransfers
                    .Where(request =>
                        accountsById.ContainsKey(
                            request.FromAccountId))
                    .Select(request =>
                        NormalizeCurrency(
                            accountsById[
                                request.FromAccountId]
                                .Currency)))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                currency => currency,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
        
        var reversedReceipts = ledgerEntries
            .Where(entry =>
                entry.TreasuryTransaction?
                    .TransactionType ==
                        TransactionTypes.Reversal &&
                entry.TreasuryTransaction?
                    .ReversesTransaction?
                    .TransactionType ==
                        TransactionTypes.CashReceipt &&
                string.Equals(
                    entry.EntryType,
                    "Credit",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        var reversedPayments = ledgerEntries
            .Where(entry =>
                entry.TreasuryTransaction?
                    .TransactionType ==
                        TransactionTypes.Reversal &&
                entry.TreasuryTransaction?
                    .ReversesTransaction?
                    .TransactionType ==
                        TransactionTypes.CashPayment &&
                string.Equals(
                    entry.EntryType,
                    "Debit",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        var currencyReports =
            new List<CurrencyLiquidityDto>();

        foreach (var currency in currencies)
        {
            var currencyAccounts =
                activeAccounts
                    .Where(account =>
                        CurrencyMatches(
                            account.Currency,
                            currency))
                    .ToList();

            var currencyTransfers =
                completedTransfers
                    .Where(entry =>
                        CurrencyMatches(
                            entry.Account.Currency,
                            currency))
                    .ToList();

            var currencyPendingTransfers =
                pendingTransfers
                    .Where(request =>
                    {
                        if (!accountsById.TryGetValue(
                            request.FromAccountId,
                            out var sourceAccount))
                        {
                            return false;
                        }

                        return CurrencyMatches(
                            sourceAccount.Currency,
                            currency);
                    })
                    .ToList();
            var currencyReceipts =
                cashReceipts
                    .Where(entry =>
                        CurrencyMatches(
                            entry.Account.Currency,
                            currency))
                    .ToList();
            
            var currencyPayments =
                cashPayments
                    .Where(entry =>
                        CurrencyMatches(
                            entry.Account.Currency,
                            currency))
                    .ToList();
            
            var currencyReversedReceipts =
                reversedReceipts
                    .Where(entry =>
                        CurrencyMatches(
                            entry.Account.Currency,
                            currency))
                    .ToList();

            var currencyReversedPayments =
                reversedPayments
                    .Where(entry =>
                        CurrencyMatches(
                            entry.Account.Currency,
                            currency))
                    .ToList();

            var balances =
                CalculateLiquidityBalances(
                    currencyAccounts);

            currencyReports.Add(
                new CurrencyLiquidityDto
                {
                    Currency = currency,

                    CurrentTotalCash =
                        balances.TotalCash,

                    AvailableLiquidity =
                        balances.AvailableLiquidity,

                    CommittedCash =
                        balances.CommittedCash,

                    InvestmentBalance =
                        balances.InvestmentBalance,

                    OtherBalance =
                        balances.OtherBalance,

                    ReservedCash =
                        balances.ReservedCash,
                    
                    ExternalReceiptCount =
                        currencyReceipts.Count,

                    ExternalReceiptAmount =
                        currencyReceipts.Sum(
                            entry => entry.Amount),
                    
                    ExternalPaymentCount =
                        currencyPayments.Count,

                    ExternalPaymentAmount =
                        currencyPayments.Sum(
                            entry => entry.Amount),

                    ReversedReceiptCount =
                        currencyReversedReceipts.Count,

                    ReversedReceiptAmount =
                        currencyReversedReceipts.Sum(
                            entry => entry.Amount),

                    ReversedPaymentCount =
                        currencyReversedPayments.Count,

                    ReversedPaymentAmount =
                        currencyReversedPayments.Sum(
                            entry => entry.Amount),

                    NetExternalCashFlow =
                        currencyReceipts.Sum(
                            entry => entry.Amount)
                        -
                        currencyReversedReceipts.Sum(
                            entry => entry.Amount)
                        -
                        currencyPayments.Sum(
                            entry => entry.Amount)
                        +
                        currencyReversedPayments.Sum(
                            entry => entry.Amount),

                    AvailableLiquidityRatio =
                        CalculateRatio(
                            balances
                                .AvailableLiquidity,
                            balances.TotalCash),

                    CompletedInternalTransferCount =
                        currencyTransfers.Count,

                    CompletedInternalTransferVolume =
                        currencyTransfers.Sum(
                            entry => entry.Amount),

                    PendingInternalTransferCount =
                        currencyPendingTransfers.Count,

                    PendingInternalTransferAmount =
                        currencyPendingTransfers.Sum(
                            request => request.Amount)
                });
        }

        return new LiquidityReportDto
        {
            ActivityFromUtc = reportFromUtc,

            ActivityToUtc = reportToUtc,

            CashPositionAsOfUtc =
                DateTime.UtcNow,

            LegalEntityId =
                filter.LegalEntityId,

            BusinessUnitId =
                filter.BusinessUnitId,

            Currencies = currencyReports
        };
    }

    public async Task<CsvExportDto> ExportLiquidityReportCsv(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? legalEntityId = null,
        Guid? businessUnitId = null)
    {
        var report =
            await GetLiquidityReport(
                fromUtc,
                toUtc,
                legalEntityId,
                businessUnitId);

        var csv = new StringBuilder();

        /*
        * This export gives Finance/Treasury a flat,
        * Excel-friendly liquidity report by currency.
        */
        csv.AppendLine(
            "ActivityFromUtc,ActivityToUtc,CashPositionAsOfUtc,LegalEntityId,BusinessUnitId,Currency,CurrentTotalCash,ReservedCash,AvailableLiquidity,CommittedCash,InvestmentBalance,OtherBalance,AvailableLiquidityRatio,ExternalReceiptCount,ExternalReceiptAmount,ExternalPaymentCount,ExternalPaymentAmount,NetExternalCashFlow,CompletedInternalTransferCount,CompletedInternalTransferVolume,PendingInternalTransferCount,PendingInternalTransferAmount,ReversedReceiptCount,ReversedReceiptAmount,ReversedPaymentCount,ReversedPaymentAmount");

        foreach (var currency in report.Currencies)
        {
            csv.AppendLine(string.Join(
                ",",
                CsvExportHelper.Escape(report.ActivityFromUtc),
                CsvExportHelper.Escape(report.ActivityToUtc),
                CsvExportHelper.Escape(report.CashPositionAsOfUtc),
                CsvExportHelper.Escape(report.LegalEntityId),
                CsvExportHelper.Escape(report.BusinessUnitId),
                CsvExportHelper.Escape(currency.Currency),
                CsvExportHelper.Escape(currency.CurrentTotalCash),
                CsvExportHelper.Escape(currency.ReservedCash),
                CsvExportHelper.Escape(currency.AvailableLiquidity),
                CsvExportHelper.Escape(currency.CommittedCash),
                CsvExportHelper.Escape(currency.InvestmentBalance),
                CsvExportHelper.Escape(currency.OtherBalance),
                CsvExportHelper.Escape(currency.AvailableLiquidityRatio),
                CsvExportHelper.Escape(currency.ExternalReceiptCount),
                CsvExportHelper.Escape(currency.ExternalReceiptAmount),
                CsvExportHelper.Escape(currency.ExternalPaymentCount),
                CsvExportHelper.Escape(currency.ExternalPaymentAmount),
                CsvExportHelper.Escape(currency.NetExternalCashFlow),
                CsvExportHelper.Escape(currency.CompletedInternalTransferCount),
                CsvExportHelper.Escape(currency.CompletedInternalTransferVolume),
                CsvExportHelper.Escape(currency.PendingInternalTransferCount),
                CsvExportHelper.Escape(currency.PendingInternalTransferAmount),
                CsvExportHelper.Escape(currency.ReversedReceiptCount),
                CsvExportHelper.Escape(currency.ReversedReceiptAmount),
                CsvExportHelper.Escape(currency.ReversedPaymentCount),
                CsvExportHelper.Escape(currency.ReversedPaymentAmount)));
        }

        var timestamp =
            DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        return new CsvExportDto
        {
            FileName =
                $"liquidity-report-{timestamp}.csv",

            ContentType =
                "text/csv",

            Content =
                CsvExportHelper.ToUtf8Bytes(
                    csv.ToString())
        };
    }
}
