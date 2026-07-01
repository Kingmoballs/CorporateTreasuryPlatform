using Treasury.Application.DTOs.Reporting;
using Treasury.Application.Interfaces;

namespace Treasury.Infrastructure.Services;

public class TreasuryReportingService
    : ITreasuryReportingService
{
    private readonly IAccountRepository
        _accountRepository;

    public TreasuryReportingService(
        IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async Task<BalanceAggregationDto>
        GetBalanceAggregation()
    {
        var accounts =
            await _accountRepository.GetAll();

        // Inactive accounts are excluded from current
        // treasury cash-position calculations.
        var activeAccounts = accounts
            .Where(account => account.IsActive)
            .ToList();

        /*
         * Each currency is aggregated independently.
         * Combining NGN, USD, and EUR without exchange
         * rates would produce an invalid total.
         */
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
                    Currency =
                        currencyGroup.Key,

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

    private static string NormalizeCurrency(
        string currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "UNKNOWN"
            : currency.Trim().ToUpperInvariant();
    }
}