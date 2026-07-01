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
        return await TransferFunds(dto, false);
    }
    public async Task<TransferResponseDto>
        TransferFunds(
            CreateTransferDto dto,
            bool skipApproval)
    {
        var fromAccount =
            await _accountRepository
                .GetById(dto.FromAccountId);

        var toAccount =
            await _accountRepository
                .GetById(dto.ToAccountId);

        if (fromAccount is null ||
            toAccount is null)
        {
            throw new Exception(
                "Invalid account selected.");
        }

        if (dto.Amount <= 0)
        {
            throw new Exception(
                "Transfer amount must be greater than zero.");
        }

        if (fromAccount.Id == toAccount.Id)
        {
            throw new Exception(
                "Source and destination accounts must be different.");
        }

        if (fromAccount.Balance < dto.Amount)
        {
            throw new Exception(
                "Insufficient funds.");
        }

        if (!skipApproval &&
            dto.Amount > ApprovalThreshold)
        {
            var request = new TransferRequest
            {
                Id = Guid.NewGuid(),
                FromAccountId = dto.FromAccountId,
                ToAccountId = dto.ToAccountId,
                Amount = dto.Amount,
                Description = dto.Description,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
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

        // The explicit transaction is only needed
        // when balances and ledger entries will change.
        await _accountRepository.BeginTransaction();

        try
        {
            fromAccount.Balance -= dto.Amount;
            toAccount.Balance += dto.Amount;

            _accountRepository.Update(fromAccount);
            _accountRepository.Update(toAccount);

            await _ledgerRepository.Add(
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    AccountId = fromAccount.Id,
                    Amount = dto.Amount,
                    EntryType = "Credit",
                    Description = dto.Description,
                    CreatedAt = DateTime.UtcNow
                });

            await _ledgerRepository.Add(
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    AccountId = toAccount.Id,
                    Amount = dto.Amount,
                    EntryType = "Debit",
                    Description = dto.Description,
                    CreatedAt = DateTime.UtcNow
                });

            await _accountRepository.SaveChanges();
            await _accountRepository.CommitTransaction();

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

    public async Task<List<TransferRequest>>
    GetPendingTransfers()
    {
        return await _transferRequestRepository
            .GetPending();
    }

    public async Task<string>
        ApproveTransfer(Guid transferId)
    {
        var request =
            await _transferRequestRepository
                .GetById(transferId);

        if (request is null)
        {
            throw new Exception(
                "Transfer request not found.");
        }

        if (request.Status != "Pending")
        {
            throw new Exception(
                "Transfer already processed.");
        }

        await TransferFunds(new CreateTransferDto
        {
            FromAccountId = request.FromAccountId,
            ToAccountId = request.ToAccountId,
            Amount = request.Amount,
            Description = request.Description
        },
        true);

        request.Status = "Approved";

        _transferRequestRepository.Update(
            request);

        await _transferRequestRepository
            .SaveChanges();

        return "Transfer approved successfully.";
    }

    public async Task<string>
        RejectTransfer(Guid transferId)
    {
        var request =
            await _transferRequestRepository
                .GetById(transferId);

        if (request is null)
        {
            throw new Exception(
                "Transfer request not found.");
        }

        if (request.Status != "Pending")
        {
            throw new Exception(
                "Transfer already processed.");
        }

        request.Status = "Rejected";

        _transferRequestRepository.Update(
            request);

        await _transferRequestRepository
            .SaveChanges();

        return "Transfer rejected successfully.";
    }
}