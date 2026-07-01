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
        GetBalanceAggregation()
    {
        var accounts =
            await _accountRepository.GetAll();

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
            Currencies = currencies
        };
    }

    public async Task<CashPositionDashboardDto>
        GetCashPositionDashboard()
    {
        var accounts =
            await _accountRepository.GetAll();

        var pendingTransfers =
            await _transferRequestRepository
                .GetPending();

        var activeAccounts = accounts
            .Where(account => account.IsActive)
            .ToList();

        var accountsById = accounts
            .ToDictionary(account => account.Id);

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

                        Balance =
                            account.Balance
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

        foreach (var account in accounts)
        {
            totalCash += account.Balance;

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
                    account.Balance;
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
            otherBalance);
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
        decimal OtherBalance);
    
    public async Task<LiquidityReportDto>
        GetLiquidityReport(
            DateTime? fromUtc,
            DateTime? toUtc)
    {
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
            await _accountRepository.GetAll();

        var ledgerEntries =
            await _ledgerRepository
                .GetByDateRange(
                    reportFromUtc,
                    reportToUtc);

        var pendingTransfers =
            await _transferRequestRepository
                .GetPending();

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
                string.Equals(
                    entry.EntryType,
                    "Credit",
                    StringComparison
                        .OrdinalIgnoreCase))
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

            Currencies = currencyReports
        };
    }
}