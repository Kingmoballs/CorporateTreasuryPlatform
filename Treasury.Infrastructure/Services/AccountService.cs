using Treasury.Application.DTOs.Accounts;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.Ledger;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Common;
using Treasury.Shared.Constants;
using Treasury.Application.Common;
using Treasury.Application.Common.Exceptions;

namespace Treasury.Infrastructure.Services;

public class AccountService : IAccountService
{
    private readonly IAccountRepository _accountRepository;

    private readonly IAccountTypeRepository
        _accountTypeRepository;

    private readonly IOrganizationStructureRepository
        _organizationStructureRepository;
    
    private readonly ILedgerRepository
    _ledgerRepository;

    private readonly ITreasuryTransactionRepository
        _transactionRepository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuditLogService
        _auditLogService;

    public AccountService(
        IAccountRepository accountRepository,
        IAccountTypeRepository accountTypeRepository,
        IOrganizationStructureRepository
            organizationStructureRepository,
        ILedgerRepository ledgerRepository,
        ITreasuryTransactionRepository transactionRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _accountRepository =
            accountRepository;

        _accountTypeRepository =
            accountTypeRepository;

        _organizationStructureRepository =
            organizationStructureRepository;

        _ledgerRepository =
            ledgerRepository;

        _transactionRepository =
            transactionRepository;

        _currentUserService =
            currentUserService;
        
        _auditLogService =
            auditLogService;
    }

    private static void ValidateAccountRequest(
        CreateAccountDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ArgumentException(
                "Account name is required.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.AccountNumber))
        {
            throw new ArgumentException(
                "Account number is required.");
        }

        if (dto.OpeningBalance < 0)
        {
            throw new ArgumentException(
                "Opening balance cannot be negative.");
        }

        if (dto.LegalEntityId == Guid.Empty)
        {
            throw new ArgumentException(
                "Legal entity is required.");
        }

        if (dto.BusinessUnitId.HasValue &&
            dto.BusinessUnitId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Business unit is invalid.");
        }

        var currency =
            dto.Currency?.Trim();

        if (string.IsNullOrWhiteSpace(currency) ||
            currency.Length != 3 ||
            !currency.All(char.IsLetter))
        {
            throw new ArgumentException(
                "Currency must be a valid " +
                "three-letter code.");
        }
    }

    public async Task<AccountResponseDto>
        CreateAccount(CreateAccountDto dto)
    {
        ValidateAccountRequest(dto);

        var normalizedAccountNumber =
            dto.AccountNumber.Trim();

        var normalizedCurrency =
            dto.Currency
                .Trim()
                .ToUpperInvariant();

        var accountType =
            await _accountTypeRepository
                .GetById(dto.AccountTypeId);

        if (accountType is null)
        {
            throw new ResourceNotFoundException(
                "Account type not found.");
        }

        var legalEntity =
            await _organizationStructureRepository
                .GetLegalEntity(
                    dto.LegalEntityId);

        if (legalEntity is null)
        {
            throw new ResourceNotFoundException(
                "Legal entity not found.");
        }

        if (!legalEntity.IsActive)
        {
            throw new BusinessRuleException(
                "Accounts can only be assigned to an " +
                "active legal entity.");
        }

        BusinessUnit? businessUnit = null;

        if (dto.BusinessUnitId.HasValue)
        {
            businessUnit =
                await _organizationStructureRepository
                    .GetBusinessUnit(
                        dto.BusinessUnitId.Value);

            if (businessUnit is null)
            {
                throw new ResourceNotFoundException(
                    "Business unit not found.");
            }

            if (!businessUnit.IsActive)
            {
                throw new BusinessRuleException(
                    "Accounts can only be assigned to " +
                    "an active business unit.");
            }

            if (businessUnit.LegalEntityId !=
                legalEntity.Id)
            {
                throw new BusinessRuleException(
                    "The business unit does not belong " +
                    "to the selected legal entity.");
            }
        }

        var accountExists =
            await _accountRepository
                .AccountNumberExists(
                    normalizedAccountNumber);

        if (accountExists)
        {
            throw new ConflictException(
                "Account number already exists.");
        }

        await _accountRepository
            .BeginTransaction();

        try
        {
            var createdAtUtc =
                DateTime.UtcNow;

            var account = new Account
            {
                Id = Guid.NewGuid(),

                OrganizationId =
                    RequireOrganizationId(),

                LegalEntityId =
                    legalEntity.Id,

                LegalEntity =
                    legalEntity,

                BusinessUnitId =
                    businessUnit?.Id,

                BusinessUnit =
                    businessUnit,

                Name = dto.Name.Trim(),

                AccountNumber =
                    normalizedAccountNumber,

                AccountTypeId =
                    dto.AccountTypeId,

                Currency =
                    normalizedCurrency,

                Balance =
                    dto.OpeningBalance,

                IsActive = true,

                CreatedAt =
                    createdAtUtc
            };

            await _accountRepository
                .Add(account);

            TreasuryTransaction?
                openingTransaction = null;

            if (dto.OpeningBalance > 0)
            {
                openingTransaction =
                    new TreasuryTransaction
                    {
                        Id = Guid.NewGuid(),

                        Reference =
                            TransactionReferenceGenerator
                                .Generate(),

                        TransactionType =
                            TransactionTypes
                                .OpeningBalance,

                        Status =
                            TransactionStatuses
                                .Completed,

                        Amount =
                            dto.OpeningBalance,

                        Currency =
                            normalizedCurrency,

                        Description =
                            $"Opening balance for " +
                            $"{account.Name}",

                        SourceAccountId =
                            null,

                        DestinationAccountId =
                            account.Id,

                        TransferRequestId =
                            null,

                        InitiatedByUserId =
                            _currentUserService.UserId,

                        CompletedByUserId =
                            _currentUserService.UserId,

                        CreatedAtUtc =
                            createdAtUtc,

                        CompletedAtUtc =
                            createdAtUtc
                    };

                await _transactionRepository
                    .Add(openingTransaction);

                /*
                * Increasing a cash asset is recorded as
                * a debit in the treasury account subledger.
                */
                await _ledgerRepository.Add(
                    new LedgerEntry
                    {
                        Id = Guid.NewGuid(),

                        TreasuryTransactionId =
                            openingTransaction.Id,

                        AccountId =
                            account.Id,

                        Amount =
                            dto.OpeningBalance,

                        EntryType =
                            "Debit",

                        Description =
                            openingTransaction
                                .Description,

                        CreatedAt =
                            createdAtUtc
                    });
            }

            /*
            * The account, transaction header and ledger
            * entry share one scoped DbContext.
            */
            await _accountRepository
                .SaveChanges();

            var response =
                new AccountResponseDto
                {
                    Id = account.Id,

                    Name = account.Name,

                    AccountNumber =
                        account.AccountNumber,

                    AccountType =
                        accountType.Name,

                    LegalEntityId =
                        account.LegalEntityId,

                    LegalEntityCode =
                        legalEntity.Code,

                    LegalEntityName =
                        legalEntity.Name,

                    BusinessUnitId =
                        account.BusinessUnitId,

                    BusinessUnitCode =
                        businessUnit?.Code,

                    BusinessUnitName =
                        businessUnit?.Name,

                    Balance =
                        account.Balance,

                    ReservedBalance =
                        account.ReservedBalance,

                    AvailableBalance =
                        account.AvailableBalance,

                    Currency =
                        account.Currency,

                    OpeningBalanceTransactionReference =
                        openingTransaction?.Reference
                };

            await RecordAccountCreatedAudit(
                account,
                accountType.Name,
                openingTransaction?.Reference);

            await _accountRepository
                .CommitTransaction();

            return response;
        }
        catch
        {
            await _accountRepository
                .RollbackTransaction();

            throw;
        }
    }


    private async Task RecordAccountCreatedAudit(
        Account account,
        string accountTypeName,
        string? openingBalanceTransactionReference)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Created,

                EntityType =
                    AuditEntityTypes.Account,

                EntityId =
                    account.Id,

                EntityReference =
                    account.AccountNumber,

                Summary =
                    $"Account {account.Name} was created.",

                AfterValues =
                    SnapshotAccount(
                        account,
                        accountTypeName,
                        openingBalanceTransactionReference),

                Metadata =
                    new
                    {
                        Module = "Accounts",
                        HasOpeningBalance =
                            account.Balance > 0,
                        OpeningBalanceTransactionReference =
                            openingBalanceTransactionReference
                    }
            });
    }

    private static object SnapshotAccount(
        Account account,
        string accountTypeName,
        string? openingBalanceTransactionReference)
    {
        return new
        {
            account.Id,
            account.Name,
            account.AccountNumber,
            AccountType = accountTypeName,
            account.AccountTypeId,
            account.LegalEntityId,
            LegalEntityCode =
                account.LegalEntity?.Code,
            account.BusinessUnitId,
            BusinessUnitCode =
                account.BusinessUnit?.Code,
            account.Balance,
            account.ReservedBalance,
            account.AvailableBalance,
            account.Currency,
            account.IsActive,
            account.CreatedAt,
            OpeningBalanceTransactionReference =
                openingBalanceTransactionReference
        };
    }

    public async Task<List<AccountResponseDto>>
        GetAccounts(
            Guid? legalEntityId = null,
            Guid? businessUnitId = null)
    {
        var filter =
            OrganizationDimensionFilter.Create(
                legalEntityId,
                businessUnitId);

        var accounts =
            filter.Apply(
                await _accountRepository
                    .GetAll());

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

                    LegalEntityId =
                        account.LegalEntityId,

                    LegalEntityCode =
                        account.LegalEntity?.Code,

                    LegalEntityName =
                        account.LegalEntity?.Name,

                    BusinessUnitId =
                        account.BusinessUnitId,

                    BusinessUnitCode =
                        account.BusinessUnit?.Code,

                    BusinessUnitName =
                        account.BusinessUnit?.Name,

                    Balance =
                        account.Balance,

                     ReservedBalance =
                        account.ReservedBalance,

                    AvailableBalance =
                        account.AvailableBalance,

                    Currency =
                        account.Currency
                })
            .ToList();
    }

    private Guid RequireOrganizationId()
    {
        var organizationId =
            _currentUserService.OrganizationId;

        if (!organizationId.HasValue ||
            organizationId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "A valid organization context is " +
                "required.");
        }

        return organizationId.Value;
    }

    public async Task<List<LedgerEntryDto>>
        GetAccountLedger(Guid accountId)
    {
        var account =
            await _accountRepository
                .GetById(accountId);

        if (account is null)
        {
            throw new ResourceNotFoundException(
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
