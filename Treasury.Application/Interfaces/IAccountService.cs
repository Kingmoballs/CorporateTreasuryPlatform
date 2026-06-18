using Treasury.Application.DTOs.Accounts;

public interface IAccountService
{
    Task<AccountResponseDto>
        CreateAccount(
            CreateAccountDto dto);

    Task<List<AccountResponseDto>>
        GetAccounts();
}