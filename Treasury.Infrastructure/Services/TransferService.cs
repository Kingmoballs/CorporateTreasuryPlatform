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
    private readonly IApprovalPolicyService
        _approvalPolicyService;

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
    
    private readonly IApprovalDecisionRepository
        _approvalDecisionRepository;

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

    private void ReserveFunds(
        Account account,
        decimal amount)
    {
        if (account.AvailableBalance < amount)
        {
            throw new BusinessRuleException(
                "Insufficient available funds.");
        }

        account.ReservedBalance += amount;

        account.ConcurrencyToken =
            Guid.NewGuid();

        _accountRepository.Update(account);
    }

    private void ReleaseReservedFunds(
        Account account,
        decimal amount)
    {
        if (account.ReservedBalance < amount)
        {
            throw new ConflictException(
                "The account does not contain the " +
                "expected transfer reservation.");
        }

        account.ReservedBalance -= amount;

        account.ConcurrencyToken =
            Guid.NewGuid();

        _accountRepository.Update(account);
    }

    public TransferService(
        IAccountRepository accountRepository,
        ILedgerRepository ledgerRepository,
        ITransferRequestRepository
            transferRequestRepository,
        ICurrentUserService currentUserService,
        ITreasuryTransactionRepository
            transactionRepository,
        IApprovalPolicyService approvalPolicyService,
        IApprovalDecisionRepository approvalDecisionRepository)
    {
        _accountRepository = accountRepository;
        _ledgerRepository = ledgerRepository;
        _transferRequestRepository =
            transferRequestRepository;
        _currentUserService =
            currentUserService;
        _transactionRepository = 
            transactionRepository;
        _approvalPolicyService =
            approvalPolicyService;
        _approvalDecisionRepository =
            approvalDecisionRepository;
    }

    public async Task<TransferResponseDto>
        TransferFunds(CreateTransferDto dto)
    {
        var accounts =
            await GetAndValidateAccounts(dto);
        
        var approvalRequirements =
            await _approvalPolicyService
                .GetRequirements(
                    ApprovalOperationTypes
                        .InternalTransfer,
                    accounts.FromAccount.Currency);

        if (dto.Amount >  approvalRequirements.ThresholdAmount)
        {
            ReserveFunds(
                accounts.FromAccount,
                dto.Amount);

            var createdAtUtc =
                DateTime.UtcNow;

            var request = new TransferRequest
            {
                Id = Guid.NewGuid(),

                FromAccountId =
                    accounts.FromAccount.Id,

                ToAccountId =
                    accounts.ToAccount.Id,

                Amount = dto.Amount,

                Description = dto.Description,

                RequiredApprovalCount =
                    approvalRequirements
                        .RequiredApprovalCount,

                ApprovalCount = 0,

                Status = ApprovalStatus.Pending,

                RequestedByUserId =
                    _currentUserService.UserId,

                ConcurrencyToken =
                    Guid.NewGuid(),
                
                CreatedAt = createdAtUtc,

                ExpiresAtUtc =
                    createdAtUtc.AddHours(
                        approvalRequirements
                            .PendingRequestExpiryHours),
            };

            try
            {
                await _transferRequestRepository
                    .Add(request);

                /*
                * The reservation and request share one DbContext,
                * so SaveChanges persists them atomically.
                */
                await _transferRequestRepository
                    .SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(
                    "The account balance changed while " +
                    "funds were being reserved.");
            }

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

                ApprovalCount =
                    request.ApprovalCount,

                RequiredApprovalCount =
                    request.RequiredApprovalCount,

                Description =
                    "Transfer pending approval.",

                Timestamp = DateTime.UtcNow,

                ExpiresAtUtc =
                    request.ExpiresAtUtc,
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
                        _currentUserService.UserId,
                    releaseReservation: false);

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

            var currentUserId =
                _currentUserService.UserId;

            var alreadyDecided =
                await _approvalDecisionRepository
                    .HasTransferDecision(
                        request.Id,
                        currentUserId);

            if (alreadyDecided)
            {
                throw new ConflictException(
                    "You have already reviewed this " +
                    "transfer request.");
            }

            await _approvalDecisionRepository.Add(
                new ApprovalDecision
                {
                    Id = Guid.NewGuid(),

                    TransferRequestId =
                        request.Id,

                    ApproverUserId =
                        currentUserId,

                    Decision =
                        ApprovalDecisionTypes
                            .Approved,

                    CreatedAtUtc =
                        DateTime.UtcNow
                });

            request.ApprovalCount += 1;

            request.ConcurrencyToken =
                Guid.NewGuid();

            /*
            * Keep the reservation while additional
            * approvals are still outstanding.
            */
            if (request.ApprovalCount <
                request.RequiredApprovalCount)
            {
                _transferRequestRepository
                    .Update(request);

                await _accountRepository
                    .SaveChanges();

                await _accountRepository
                    .CommitTransaction();

                return CreatePendingTransferResponse(
                    request);
            }

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
                await GetAndValidateAccounts(
                    dto,
                    fundsAlreadyReserved: true);

            var transaction =
                await ApplyTransfer(
                    accounts.FromAccount,
                    accounts.ToAccount,
                    request.Amount,
                    request.Description,
                    transferRequestId:
                        request.Id,
                    initiatedByUserId:
                        request.RequestedByUserId,
                    releaseReservation:
                        true);

            request.Status =
                ApprovalStatus.Approved;

            request.ReviewedByUserId =
                currentUserId;

            request.ReviewedAtUtc =
                DateTime.UtcNow;

            request.RejectionReason =
                null;

            request.ConcurrencyToken =
                Guid.NewGuid();

            _transferRequestRepository
                .Update(request);

            await _accountRepository
                .SaveChanges();

            await _accountRepository
                .CommitTransaction();

            var response =
                CreateResponse(
                    accounts.FromAccount.Id,
                    accounts.ToAccount.Id,
                    request.Amount,
                    request.Description,
                    transaction);

            response.ApprovalCount =
                request.ApprovalCount;

            response.RequiredApprovalCount =
                request.RequiredApprovalCount;

            return response;
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "The request or account changed while " +
                "approval was processing. Refresh and " +
                "try again.");
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
            throw new RequestValidationException(
                "A rejection reason is required.");
        }

        await _accountRepository
            .BeginTransaction();

        try
        {
            var request =
                await GetPendingRequest(
                    transferId);

            EnsureDifferentReviewer(
                request.RequestedByUserId);

            var currentUserId =
                _currentUserService.UserId;

            var alreadyDecided =
                await _approvalDecisionRepository
                    .HasTransferDecision(
                        request.Id,
                        currentUserId);

            if (alreadyDecided)
            {
                throw new ConflictException(
                    "You have already reviewed this " +
                    "transfer request.");
            }

            await _approvalDecisionRepository.Add(
                new ApprovalDecision
                {
                    Id = Guid.NewGuid(),

                    TransferRequestId =
                        request.Id,

                    ApproverUserId =
                        currentUserId,

                    Decision =
                        ApprovalDecisionTypes.Rejected,

                    Comment =
                        reason.Trim(),

                    CreatedAtUtc =
                        DateTime.UtcNow
                });

            var sourceAccount =
                await _accountRepository
                    .GetById(
                        request.FromAccountId);

            if (sourceAccount is null)
            {
                throw new ResourceNotFoundException(
                    "Source account not found.");
            }

            ReleaseReservedFunds(
                sourceAccount,
                request.Amount);

            request.Status =
                ApprovalStatus.Rejected;

            request.ReviewedByUserId =
                currentUserId;

            request.ReviewedAtUtc =
                DateTime.UtcNow;

            request.RejectionReason =
                reason.Trim();

            request.ConcurrencyToken =
                Guid.NewGuid();

            _transferRequestRepository
                .Update(request);

            await _accountRepository
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
                "The request or account reservation " +
                "changed while processing.");
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

        PendingRequestExpiryGuard.EnsureNotExpired(
            request.ExpiresAtUtc,
            "transfer");
            
        return request;
    }

    private async Task<(
        Account FromAccount,
        Account ToAccount)>
        GetAndValidateAccounts(
            CreateTransferDto dto,
            bool fundsAlreadyReserved = false)
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

        if (fundsAlreadyReserved &&
            fromAccount.ReservedBalance <
                dto.Amount)
        {
            throw new ConflictException(
                "The expected transfer reservation " +
                "was not found.");
        }

        var spendableBalance =
            fromAccount.AvailableBalance
            +
            (fundsAlreadyReserved
                ? dto.Amount
                : 0);

        if (spendableBalance < dto.Amount)
        {
            throw new BusinessRuleException(
                "Insufficient available funds.");
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
            Guid? initiatedByUserId,
            bool releaseReservation)
    {
        if (releaseReservation)
        {
            if (fromAccount.ReservedBalance <
                amount)
            {
                throw new ConflictException(
                    "Transfer reservation is missing.");
            }

            fromAccount.ReservedBalance -=
                amount;
        }

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

    private static TransferResponseDto
        CreatePendingTransferResponse(
            TransferRequest request)
    {
        return new TransferResponseDto
        {
            TransactionId =
                null,

            TransactionReference =
                null,

            Status =
                ApprovalStatus.Pending,

            FromAccountId =
                request.FromAccountId,

            ToAccountId =
                request.ToAccountId,

            Amount =
                request.Amount,

            Description =
                "Transfer is awaiting additional " +
                "approvals.",

            ApprovalCount =
                request.ApprovalCount,

            RequiredApprovalCount =
                request.RequiredApprovalCount,

            Timestamp =
                DateTime.UtcNow
        };
    }

}