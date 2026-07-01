using Treasury.Application.DTOs.Transfers;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;

namespace Treasury.Infrastructure.Services;
public class TransferService : ITransferService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly ITransferRequestRepository _transferRequestRepository;

    private const decimal ApprovalThreshold = 10000000m;

    public TransferService(
        IAccountRepository accountRepository,
        ILedgerRepository ledgerRepository,
        ITransferRequestRepository transferRequestRepository)
    {
        _accountRepository = accountRepository;
        _ledgerRepository = ledgerRepository;
        _transferRequestRepository = transferRequestRepository;
    }
    
    public async Task<TransferResponseDto>
    TransferFunds(CreateTransferDto dto)
    {
        await _accountRepository
            .BeginTransaction();

        try
        {
            var fromAccount =
                await _accountRepository
                    .GetById(dto.FromAccountId);

            var toAccount =
                await _accountRepository
                    .GetById(dto.ToAccountId);

            if (fromAccount is null || toAccount is null)
            {
                throw new Exception(
                    "Invalid account selected.");
            }

            if (dto.Amount <= 0)
            {
                throw new Exception(
                    "Transfer amount must be greater than zero.");
            }

            if (fromAccount.Balance < dto.Amount)
            {
                throw new Exception(
                    "Insufficient funds.");
            }

            if (dto.Amount > ApprovalThreshold)
            {
                var request = new TransferRequest
                {
                    Id = Guid.NewGuid(),
                    FromAccountId = dto.FromAccountId,
                    ToAccountId = dto.ToAccountId,
                    Amount = dto.Amount,
                    Description = dto.Description,
                    Status = "Pending"
                };

                await _transferRequestRepository
                    .Add(request);

                await _transferRequestRepository
                    .SaveChanges();

                return new TransferResponseDto
                {
                    FromAccountId = dto.FromAccountId,
                    ToAccountId = dto.ToAccountId,
                    Amount = dto.Amount,
                    Description =
                        "Transfer pending approval.",
                    Timestamp = DateTime.UtcNow
                };
            }

            // Move balances
            fromAccount.Balance -= dto.Amount;
            toAccount.Balance += dto.Amount;

            _accountRepository.Update(fromAccount);
            _accountRepository.Update(toAccount);

            // Credit source
            await _ledgerRepository.Add(
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    AccountId = fromAccount.Id,
                    Amount = dto.Amount,
                    EntryType = "Credit",
                    Description = dto.Description
                });

            // Debit destination
            await _ledgerRepository.Add(
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    AccountId = toAccount.Id,
                    Amount = dto.Amount,
                    EntryType = "Debit",
                    Description = dto.Description
                });

            await _accountRepository
                .SaveChanges();

            await _accountRepository
                .CommitTransaction();

            return new TransferResponseDto
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                Amount = dto.Amount,
                Description = dto.Description,
                Timestamp = DateTime.UtcNow
            };
        }
        catch
        {
            await _accountRepository
                .RollbackTransaction();

            throw;
        }
    }
}