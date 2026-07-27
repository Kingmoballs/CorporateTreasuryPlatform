using Treasury.Application.DTOs.Accounts;
using Treasury.Application.DTOs.Ledger;

public interface IAccountService
{
    Task<AccountResponseDto>
        CreateAccount(
            CreateAccountDto dto);

    Task<List<AccountResponseDto>>
        GetAccounts(
            Guid? legalEntityId = null,
            Guid? businessUnitId = null);

    Task<List<LedgerEntryDto>>
    GetAccountLedger(Guid accountId);
}
