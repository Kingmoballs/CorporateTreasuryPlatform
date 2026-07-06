using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Reversals;
using Treasury.Application.DTOs.Transactions;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Common;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class ReversalService : IReversalService
{
    private readonly IAccountRepository
        _accountRepository;

    private readonly ILedgerRepository
        _ledgerRepository;

    private readonly ITreasuryTransactionRepository
        _transactionRepository;

    private readonly IReversalRequestRepository
        _reversalRequestRepository;

    private readonly ITreasuryTransactionService
        _transactionService;

    private readonly ICurrentUserService
        _currentUserService;

    public ReversalService(
        IAccountRepository accountRepository,
        ILedgerRepository ledgerRepository,
        ITreasuryTransactionRepository
            transactionRepository,
        IReversalRequestRepository
            reversalRequestRepository,
        ITreasuryTransactionService
            transactionService,
        ICurrentUserService currentUserService)
    {
        _accountRepository = accountRepository;
        _ledgerRepository = ledgerRepository;
        _transactionRepository =
            transactionRepository;
        _reversalRequestRepository =
            reversalRequestRepository;
        _transactionService =
            transactionService;
        _currentUserService =
            currentUserService;
    }

    public async Task<ReversalRequestResponseDto>
        RequestReversal(
            string transactionReference,
            string reason)
    {
        ValidateReason(reason);

        var original =
            await _transactionRepository
                .GetByReference(
                    transactionReference
                        .Trim()
                        .ToUpperInvariant());

        if (original is null)
        {
            throw new ResourceNotFoundException(
                "Transaction not found.");
        }

        EnsureTransactionCanBeReversed(
            original);

        var existingRequest =
            await _reversalRequestRepository
                .GetByOriginalTransactionId(
                    original.Id);

        if (existingRequest is not null)
        {
            throw new ConflictException(
                "A reversal request already exists " +
                "for this transaction.");
        }

        var existingReversal =
            await _transactionRepository
                .GetByReversedTransactionId(
                    original.Id);

        if (existingReversal is not null)
        {
            throw new ConflictException(
                "This transaction has already " +
                "been reversed.");
        }

        var request = new ReversalRequest
        {
            Id = Guid.NewGuid(),

            OriginalTransactionId =
                original.Id,

            Reason =
                reason.Trim(),

            Status =
                ApprovalStatus.Pending,

            RequestedByUserId =
                _currentUserService.UserId,

            ConcurrencyToken =
                Guid.NewGuid(),

            CreatedAtUtc =
                DateTime.UtcNow
        };

        await _reversalRequestRepository
            .Add(request);

        await _reversalRequestRepository
            .SaveChanges();

        return MapRequest(
            request,
            original);
    }

    public async Task<List<ReversalRequestResponseDto>>
        GetPending()
    {
        var requests =
            await _reversalRequestRepository
                .GetPending();

        return requests
            .Select(request =>
                MapRequest(
                    request,
                    request.OriginalTransaction))
            .ToList();
    }

    public async Task<TreasuryTransactionDetailDto>
        Approve(Guid reversalRequestId)
    {
        await _accountRepository
            .BeginTransaction();

        try
        {
            var request =
                await GetPendingRequest(
                    reversalRequestId);

            EnsureDifferentReviewer(
                request.RequestedByUserId);

            var existingReversal =
                await _transactionRepository
                    .GetByReversedTransactionId(
                        request
                            .OriginalTransactionId);

            if (existingReversal is not null)
            {
                throw new ConflictException(
                    "This transaction has already " +
                    "been reversed.");
            }

            var reversal =
                await ApplyReversal(request);

            request.Status =
                ApprovalStatus.Approved;

            request.ReviewedByUserId =
                _currentUserService.UserId;

            request.ReviewedAtUtc =
                DateTime.UtcNow;

            request.ConcurrencyToken =
                Guid.NewGuid();

            _reversalRequestRepository
                .Update(request);

            await _accountRepository
                .SaveChanges();

            await _accountRepository
                .CommitTransaction();

            return await _transactionService
                .GetByReference(
                    reversal.Reference);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "The reversal request or account " +
                "balance changed while processing.");
        }
        catch
        {
            await _accountRepository
                .RollbackTransaction();

            throw;
        }
    }

    public async Task<ReversalRequestResponseDto>
        Reject(
            Guid reversalRequestId,
            string reason)
    {
        ValidateReason(reason);

        var request =
            await GetPendingRequest(
                reversalRequestId);

        EnsureDifferentReviewer(
            request.RequestedByUserId);

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

        _reversalRequestRepository
            .Update(request);

        try
        {
            await _reversalRequestRepository
                .SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The reversal request was already " +
                "processed by another user.");
        }

        return MapRequest(
            request,
            request.OriginalTransaction);
    }

    private async Task<TreasuryTransaction>
        ApplyReversal(
            ReversalRequest request)
    {
        var original =
            request.OriginalTransaction;

        var completedAtUtc =
            DateTime.UtcNow;

        var reversal =
            new TreasuryTransaction
            {
                Id = Guid.NewGuid(),

                Reference =
                    TransactionReferenceGenerator
                        .Generate(),

                TransactionType =
                    TransactionTypes.Reversal,

                Status =
                    TransactionStatuses.Completed,

                Amount =
                    original.Amount,

                Currency =
                    original.Currency,

                Category =
                    original.Category,

                CounterpartyName =
                    original.CounterpartyName,

                ExternalReference =
                    original.Reference,

                IdempotencyKey =
                    $"reversal:{original.Id:N}",

                Description =
                    $"Reversal of " +
                    $"{original.Reference}: " +
                    $"{request.Reason}",

                ReversesTransactionId =
                    original.Id,

                ReversalRequestId =
                    request.Id,

                InitiatedByUserId =
                    request.RequestedByUserId,

                CompletedByUserId =
                    _currentUserService.UserId,

                CreatedAtUtc =
                    completedAtUtc,

                CompletedAtUtc =
                    completedAtUtc
            };

        switch (original.TransactionType)
        {
            case TransactionTypes.InternalTransfer:
                await ReverseInternalTransfer(
                    original,
                    reversal,
                    completedAtUtc);
                break;

            case TransactionTypes.CashReceipt:
                await ReverseCashReceipt(
                    original,
                    reversal,
                    completedAtUtc);
                break;

            case TransactionTypes.CashPayment:
                await ReverseCashPayment(
                    original,
                    reversal,
                    completedAtUtc);
                break;

            default:
                throw new BusinessRuleException(
                    "This transaction type cannot " +
                    "be reversed.");
        }

        await _transactionRepository
            .Add(reversal);

        return reversal;
    }

    private async Task ReverseInternalTransfer(
        TreasuryTransaction original,
        TreasuryTransaction reversal,
        DateTime createdAtUtc)
    {
        var originalSource =
            await GetRequiredAccount(
                original.SourceAccountId);

        var originalDestination =
            await GetRequiredAccount(
                original.DestinationAccountId);

        if (originalDestination.Balance <
            original.Amount)
        {
            throw new BusinessRuleException(
                "The destination account does not " +
                "have enough funds for reversal.");
        }

        originalDestination.Balance -=
            original.Amount;

        originalSource.Balance +=
            original.Amount;

        RotateAndUpdate(
            originalDestination);

        RotateAndUpdate(
            originalSource);

        reversal.SourceAccountId =
            originalDestination.Id;

        reversal.DestinationAccountId =
            originalSource.Id;

        await AddLedgerEntry(
            reversal.Id,
            originalDestination.Id,
            original.Amount,
            "Credit",
            reversal.Description,
            createdAtUtc);

        await AddLedgerEntry(
            reversal.Id,
            originalSource.Id,
            original.Amount,
            "Debit",
            reversal.Description,
            createdAtUtc);
    }

    private async Task ReverseCashReceipt(
        TreasuryTransaction original,
        TreasuryTransaction reversal,
        DateTime createdAtUtc)
    {
        var account =
            await GetRequiredAccount(
                original.DestinationAccountId);

        if (account.AvailableBalance <
            original.Amount)
        {
            throw new BusinessRuleException(
                "The receipt account does not have " +
                "enough funds for reversal.");
        }

        account.Balance -= original.Amount;

        RotateAndUpdate(account);

        reversal.SourceAccountId =
            account.Id;

        reversal.DestinationAccountId =
            null;

        await AddLedgerEntry(
            reversal.Id,
            account.Id,
            original.Amount,
            "Credit",
            reversal.Description,
            createdAtUtc);
    }

    private async Task ReverseCashPayment(
        TreasuryTransaction original,
        TreasuryTransaction reversal,
        DateTime createdAtUtc)
    {
        var account =
            await GetRequiredAccount(
                original.SourceAccountId);

        account.Balance += original.Amount;

        RotateAndUpdate(account);

        reversal.SourceAccountId =
            null;

        reversal.DestinationAccountId =
            account.Id;

        await AddLedgerEntry(
            reversal.Id,
            account.Id,
            original.Amount,
            "Debit",
            reversal.Description,
            createdAtUtc);
    }

    private async Task AddLedgerEntry(
        Guid transactionId,
        Guid accountId,
        decimal amount,
        string entryType,
        string description,
        DateTime createdAtUtc)
    {
        await _ledgerRepository.Add(
            new LedgerEntry
            {
                Id = Guid.NewGuid(),

                TreasuryTransactionId =
                    transactionId,

                AccountId =
                    accountId,

                Amount =
                    amount,

                EntryType =
                    entryType,

                Description =
                    description,

                CreatedAt =
                    createdAtUtc
            });
    }

    private async Task<Account>
        GetRequiredAccount(Guid? accountId)
    {
        if (!accountId.HasValue)
        {
            throw new BusinessRuleException(
                "The original transaction does not " +
                "contain the required account.");
        }

        var account =
            await _accountRepository
                .GetById(accountId.Value);

        if (account is null)
        {
            throw new ResourceNotFoundException(
                "Transaction account not found.");
        }

        if (!account.IsActive)
        {
            throw new BusinessRuleException(
                "Reversals require active accounts.");
        }

        return account;
    }

    private void RotateAndUpdate(Account account)
    {
        account.ConcurrencyToken =
            Guid.NewGuid();

        _accountRepository.Update(account);
    }

    private async Task<ReversalRequest>
        GetPendingRequest(Guid requestId)
    {
        var request =
            await _reversalRequestRepository
                .GetById(requestId);

        if (request is null)
        {
            throw new ResourceNotFoundException(
                "Reversal request not found.");
        }

        if (request.Status !=
            ApprovalStatus.Pending)
        {
            throw new ConflictException(
                "Reversal request has already " +
                "been processed.");
        }

        return request;
    }

    private void EnsureDifferentReviewer(
        Guid requestedByUserId)
    {
        if (requestedByUserId ==
            _currentUserService.UserId)
        {
            throw new ForbiddenOperationException(
                "You cannot review your own " +
                "reversal request.");
        }
    }

    private static void
        EnsureTransactionCanBeReversed(
            TreasuryTransaction transaction)
    {
        var reversible =
            transaction.TransactionType ==
                TransactionTypes.InternalTransfer ||
            transaction.TransactionType ==
                TransactionTypes.CashReceipt ||
            transaction.TransactionType ==
                TransactionTypes.CashPayment;

        if (!reversible)
        {
            throw new BusinessRuleException(
                "This transaction type cannot " +
                "be reversed.");
        }
    }

    private static void ValidateReason(
        string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new RequestValidationException(
                "A reversal reason is required.");
        }
    }

    private static ReversalRequestResponseDto
        MapRequest(
            ReversalRequest request,
            TreasuryTransaction original)
    {
        if (original is null)
        {
            throw new InvalidOperationException(
                "Original transaction was not loaded.");
        }

        return new ReversalRequestResponseDto
        {
            Id = request.Id,

            OriginalTransactionId =
                request.OriginalTransactionId,

            OriginalTransactionReference =
                original.Reference,

            Amount =
                original.Amount,

            Currency =
                original.Currency,

            Reason =
                request.Reason,

            Status =
                request.Status,

            RequestedByUserId =
                request.RequestedByUserId,

            ReviewedByUserId =
                request.ReviewedByUserId,

            ApprovalCount =
                request.ApprovalCount,

            RequiredApprovalCount =
                request.RequiredApprovalCount,

            ReviewedAtUtc =
                request.ReviewedAtUtc,

            RejectionReason =
                request.RejectionReason,

            CreatedAtUtc =
                request.CreatedAtUtc
        };
    }
}