using Treasury.Application.DTOs.Accounts;
using Treasury.Application.DTOs.Ledger;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;

namespace Treasury.Infrastructure.Services;

public class AccountService : IAccountService
{
    private readonly IAccountRepository _accountRepository;

    private readonly IAccountTypeRepository
        _accountTypeRepository;
    
    private readonly ILedgerRepository
    _ledgerRepository;

    public AccountService(
        IAccountRepository accountRepository,
        IAccountTypeRepository accountTypeRepository,
        ILedgerRepository ledgerRepository)
    {
        _accountRepository = accountRepository;
        _accountTypeRepository = accountTypeRepository;
        _ledgerRepository = ledgerRepository;
    }

    public async Task<AccountResponseDto>
        CreateAccount(CreateAccountDto dto)
    {
        // Verify Account Type Exists
        var accountType =
            await _accountTypeRepository
                .GetById(dto.AccountTypeId);

        if (accountType is null)
        {
            throw new Exception(
                "Account type not found.");
        }

        // Verify Account Number Is Unique
        var accountExists =
            await _accountRepository
                .AccountNumberExists(
                    dto.AccountNumber);

        if (accountExists)
        {
            throw new Exception(
                "Account number already exists.");
        }

        // Create Account
        var account = new Account
        {
            Id = Guid.NewGuid(),

            Name = dto.Name,

            AccountNumber =
                dto.AccountNumber,

            AccountTypeId =
                dto.AccountTypeId,

            Currency =
                dto.Currency,

            Balance =
                dto.OpeningBalance,

            IsActive = true
        };

        await _accountRepository
            .Add(account);

        await _accountRepository
            .SaveChanges();

        return new AccountResponseDto
        {
            Id = account.Id,

            Name = account.Name,

            AccountNumber =
                account.AccountNumber,

            AccountType =
                accountType.Name,

            Balance =
                account.Balance,

            Currency =
                account.Currency
        };
    }

    public async Task<List<AccountResponseDto>>
        GetAccounts()
    {
        var accounts =
            await _accountRepository
                .GetAll();

        return accounts
            .Select(account =>
                new AccountResponseDto
                {
                    Id = account.Id,

                    Name = account.Name,

                    AccountNumber =
                        account.AccountNumber,

                    AccountType =
                        account.AccountType.Name,

                    Balance =
                        account.Balance,

                    Currency =
                        account.Currency
                })
            .ToList();
    }

    public async Task<List<LedgerEntryDto>>
        GetAccountLedger(Guid accountId)
    {
        var account =
            await _accountRepository
                .GetById(accountId);

        if (account is null)
        {
            throw new Exception(
                "Account not found.");
        }

        var entries =
            await _ledgerRepository
                .GetByAccountId(accountId);

        return entries
            .Select(x => new LedgerEntryDto
            {
                TransactionReference = x.TreasuryTransaction?.Reference,
                Amount = x.Amount,
                EntryType = x.EntryType,
                Description = x.Description,
                CreatedAt = x.CreatedAt
            })
            .ToList();
    }
}