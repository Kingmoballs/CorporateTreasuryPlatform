using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.Transfers;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;
using Treasury.Shared.Common;
using Treasury.Application.Common.Exceptions;

namespace Treasury.Infrastructure.Services;

public class TransferService : ITransferService
{
    private const decimal ApprovalThreshold =
        10000000m;

    private readonly IAccountRepository
        _accountRepository;

    private readonly ILedgerRepository
        _ledgerRepository;

    private readonly ITransferRequestRepository
        _transferRequestRepository;

    private readonly ICurrentUserService
        _currentUserService;
    
    private readonly ITreasuryTransactionRepository
        _transactionRepository;
    private void EnsureDifferentReviewer(
        Guid? requestedByUserId)
    {
        /*
        * Older requests may not have requester metadata,
        * so maker-checker applies when the ID is available.
        */
        if (requestedByUserId.HasValue &&
            requestedByUserId.Value ==
                _currentUserService.UserId)
        {
            throw new ForbiddenOperationException(
                "You cannot approve or reject " +
                "your own transfer request.");
        }
    }

    public TransferService(
        IAccountRepository accountRepository,
        ILedgerRepository ledgerRepository,
        ITransferRequestRepository
            transferRequestRepository,
        ICurrentUserService currentUserService,
        ITreasuryTransactionRepository
            transactionRepository)
    {
        _accountRepository = accountRepository;
        _ledgerRepository = ledgerRepository;
        _transferRequestRepository =
            transferRequestRepository;
        _currentUserService =
            currentUserService;
        _transactionRepository = 
            transactionRepository;
    }

    public async Task<TransferResponseDto>
        TransferFunds(CreateTransferDto dto)
    {
        var accounts =
            await GetAndValidateAccounts(dto);

        if (dto.Amount > ApprovalThreshold)
        {
            var request = new TransferRequest
            {
                Id = Guid.NewGuid(),

                FromAccountId =
                    accounts.FromAccount.Id,

                ToAccountId =
                    accounts.ToAccount.Id,

                Amount = dto.Amount,

                Description = dto.Description,

                Status = ApprovalStatus.Pending,

                RequestedByUserId =
                    _currentUserService.UserId,

                ConcurrencyToken =
                    Guid.NewGuid(),

                CreatedAt = DateTime.UtcNow
            };

            await _transferRequestRepository
                .Add(request);

            await _transferRequestRepository
                .SaveChanges();

            return new TransferResponseDto
            {
                FromAccountId =
                    request.FromAccountId,

                ToAccountId =
                    request.ToAccountId,

                Amount = request.Amount,

                TransactionId = null,

                TransactionReference = null,

                Status = ApprovalStatus.Pending,

                Description =
                    "Transfer pending approval.",

                Timestamp = DateTime.UtcNow
            };
        }

        await _accountRepository
            .BeginTransaction();

        try
        {
            var transaction =
                await ApplyTransfer(
                    accounts.FromAccount,
                    accounts.ToAccount,
                    dto.Amount,
                    dto.Description,
                    transferRequestId: null,
                    initiatedByUserId:
                        _currentUserService.UserId);

            await _accountRepository
                .SaveChanges();

            await _accountRepository
                .CommitTransaction();

            return CreateResponse(
                accounts.FromAccount.Id,
                accounts.ToAccount.Id,
                dto.Amount,
                dto.Description,
                transaction);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "An account balance changed while the " +
                "transfer was processing. Refresh the " +
                "account and try again.");
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

    public async Task<TransferResponseDto>
        ApproveTransfer(Guid transferId)
    {
        await _accountRepository
            .BeginTransaction();

        try
        {
            var request =
                await GetPendingRequest(
                    transferId);
            
            EnsureDifferentReviewer(
                request.RequestedByUserId);

            var dto = new CreateTransferDto
            {
                FromAccountId =
                    request.FromAccountId,

                ToAccountId =
                    request.ToAccountId,

                Amount =
                    request.Amount,

                Description =
                    request.Description
            };

            var accounts =
                await GetAndValidateAccounts(dto);

            var transaction =
                await ApplyTransfer(
                    accounts.FromAccount,
                    accounts.ToAccount,
                    request.Amount,
                    request.Description,
                    transferRequestId:
                        request.Id,
                    initiatedByUserId:
                        request.RequestedByUserId);

            request.Status =
                ApprovalStatus.Approved;

            request.ReviewedByUserId =
                _currentUserService.UserId;

            request.ReviewedAtUtc =
                DateTime.UtcNow;

            request.RejectionReason = null;

            // Rotating the token makes a concurrent update fail.
            request.ConcurrencyToken =
                Guid.NewGuid();

            _transferRequestRepository
                .Update(request);

            /*
             * All repositories share the same scoped
             * TreasuryDbContext. One SaveChanges therefore
             * saves the balances, ledger and request status.
             */
            await _accountRepository
                .SaveChanges();

            await _accountRepository
                .CommitTransaction();

            return CreateResponse(
                accounts.FromAccount.Id,
                accounts.ToAccount.Id,
                request.Amount,
                request.Description,
                transaction);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "The transfer request or an account " +
                "balance changed while approval was " +
                "processing. Refresh and try again.");
        }
        catch
        {
            await _accountRepository
                .RollbackTransaction();

            throw;
        }
    }

    public async Task<string>
        RejectTransfer(
            Guid transferId,
            string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "A rejection reason is required.");
        }

        await _accountRepository
            .BeginTransaction();

        try
        {
            var request =
                await GetPendingRequest(
                    transferId);

            request.Status =
                ApprovalStatus.Rejected;

            request.ReviewedByUserId =
                _currentUserService.UserId;

            request.ReviewedAtUtc =
                DateTime.UtcNow;

            request.RejectionReason =
                reason.Trim();

            request.ConcurrencyToken =
                Guid.NewGuid();

            _transferRequestRepository
                .Update(request);

            await _transferRequestRepository
                .SaveChanges();

            await _accountRepository
                .CommitTransaction();

            return
                "Transfer rejected successfully.";
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "This transfer request was already " +
                "processed by another user.");
        }
        catch
        {
            await _accountRepository
                .RollbackTransaction();

            throw;
        }
    }

    private async Task<TransferRequest>
        GetPendingRequest(Guid transferId)
    {
        var request =
            await _transferRequestRepository
                .GetById(transferId);

        if (request is null)
        {
            throw new ResourceNotFoundException(
                "Transfer request not found.");
        }

        if (!string.Equals(
            request.Status,
            ApprovalStatus.Pending,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "Transfer request has already " +
                "been processed.");
        }

        return request;
    }

    private async Task<(
        Account FromAccount,
        Account ToAccount)>
        GetAndValidateAccounts(
            CreateTransferDto dto)
    {
        if (dto.Amount <= 0)
        {
            throw new ArgumentException(
                "Transfer amount must be " +
                "greater than zero.");
        }

        if (dto.FromAccountId ==
            dto.ToAccountId)
        {
            throw new ArgumentException(
                "Source and destination accounts " +
                "must be different.");
        }

        var fromAccount =
            await _accountRepository
                .GetById(dto.FromAccountId);

        var toAccount =
            await _accountRepository
                .GetById(dto.ToAccountId);

        if (fromAccount is null ||
            toAccount is null)
        {
            throw new ResourceNotFoundException(
                "Invalid account selected.");
        }

        if (!fromAccount.IsActive ||
            !toAccount.IsActive)
        {
            throw new ConflictException(
                "Transfers require active accounts.");
        }

        /*
         * Cross-currency transfers require an FX rate
         * and must not silently move equal nominal values.
         */
        if (!string.Equals(
            fromAccount.Currency,
            toAccount.Currency,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "Cross-currency transfers are not " +
                "currently supported.");
        }

        if (fromAccount.Balance < dto.Amount)
        {
            throw new BusinessRuleException(
                "Insufficient funds.");
        }

        return (
            fromAccount,
            toAccount);
    }

    private async Task<TreasuryTransaction>
        ApplyTransfer(
            Account fromAccount,
            Account toAccount,
            decimal amount,
            string description,
            Guid? transferRequestId,
            Guid? initiatedByUserId)
    {
        var completedAtUtc =
            DateTime.UtcNow;

        var transaction =
            new TreasuryTransaction
            {
                Id = Guid.NewGuid(),

                Reference =
                    TransactionReferenceGenerator.Generate(),

                TransactionType =
                    TransactionTypes
                        .InternalTransfer,

                Status =
                    TransactionStatuses.Completed,

                Amount = amount,

                Currency =
                    fromAccount.Currency
                        .Trim()
                        .ToUpperInvariant(),

                Description = description,

                SourceAccountId =
                    fromAccount.Id,

                DestinationAccountId =
                    toAccount.Id,

                TransferRequestId =
                    transferRequestId,

                InitiatedByUserId =
                    initiatedByUserId,

                CompletedByUserId =
                    _currentUserService.UserId,

                CreatedAtUtc =
                    completedAtUtc,

                CompletedAtUtc =
                    completedAtUtc
            };

        await _transactionRepository
            .Add(transaction);

        fromAccount.Balance -= amount;
        toAccount.Balance += amount;

        // Rotating both tokens makes stale balance updates fail.
        fromAccount.ConcurrencyToken =
            Guid.NewGuid();

        toAccount.ConcurrencyToken =
            Guid.NewGuid();

        _accountRepository.Update(fromAccount);
        _accountRepository.Update(toAccount);

        await _ledgerRepository.Add(
            new LedgerEntry
            {
                Id = Guid.NewGuid(),

                TreasuryTransactionId =
                    transaction.Id,

                AccountId =
                    fromAccount.Id,

                Amount = amount,

                EntryType = "Credit",

                Description = description,

                CreatedAt =
                    completedAtUtc
            });

        await _ledgerRepository.Add(
            new LedgerEntry
            {
                Id = Guid.NewGuid(),

                TreasuryTransactionId =
                    transaction.Id,

                AccountId =
                    toAccount.Id,

                Amount = amount,

                EntryType = "Debit",

                Description = description,

                CreatedAt =
                    completedAtUtc
            });

        return transaction;
    }

    private static TransferResponseDto
        CreateResponse(
            Guid fromAccountId,
            Guid toAccountId,
            decimal amount,
            string description,
            TreasuryTransaction transaction)
    {
        return new TransferResponseDto
        {
            TransactionId =
                transaction.Id,

            TransactionReference =
                transaction.Reference,

            Status =
                transaction.Status,

            FromAccountId =
                fromAccountId,

            ToAccountId =
                toAccountId,

            Amount =
                amount,

            Description =
                description,

            Timestamp =
                transaction.CompletedAtUtc
                ?? transaction.CreatedAtUtc
        };
    }

}