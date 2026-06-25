using Treasury.Application.DTOs.Accounts;
using Treasury.Application.DTOs.Ledger;

public interface IAccountService
{
    Task<AccountResponseDto>
        CreateAccount(
            CreateAccountDto dto);

    Task<List<AccountResponseDto>>
        GetAccounts();

    Task<List<LedgerEntryDto>>
    GetAccountLedger(Guid accountId);
}