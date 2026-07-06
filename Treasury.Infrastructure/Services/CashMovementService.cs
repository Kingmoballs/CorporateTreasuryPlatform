using Treasury.Application.DTOs.CashMovements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Common;
using Treasury.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;

namespace Treasury.Infrastructure.Services;

public class CashMovementService
    : ICashMovementService
{
    private readonly IAccountRepository
        _accountRepository;

    private readonly ILedgerRepository
        _ledgerRepository;

    private readonly ITreasuryTransactionRepository
        _transactionRepository;

    private readonly ICurrentUserService
        _currentUserService;
    
    private readonly IApprovalPolicyService
        _approvalPolicyService;

    private readonly IPaymentRequestRepository
        _paymentRequestRepository;
    
    private readonly IApprovalDecisionRepository
        _approvalDecisionRepository;
    
    private void EnsureDifferentReviewer(
        Guid requestedByUserId,
        string requestType)
    {
        if (requestedByUserId ==
            _currentUserService.UserId)
        {
            throw new ForbiddenOperationException(
                $"You cannot approve or reject " +
                $"your own {requestType} request.");
        }
    }

    private void ReservePaymentFunds(
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

    private void ReleasePaymentFunds(
        Account account,
        decimal amount)
    {
        if (account.ReservedBalance < amount)
        {
            throw new ConflictException(
                "The expected payment reservation " +
                "was not found.");
        }

        account.ReservedBalance -= amount;

        account.ConcurrencyToken =
            Guid.NewGuid();

        _accountRepository.Update(account);
    }

    public CashMovementService(
        IAccountRepository accountRepository,
        ILedgerRepository ledgerRepository,
        ITreasuryTransactionRepository
            transactionRepository,
        ICurrentUserService currentUserService,
        IPaymentRequestRepository paymentRequestRepository,
        IApprovalPolicyService approvalPolicyService,
        IApprovalDecisionRepository approvalDecisionRepository)
    {
        _accountRepository =
            accountRepository;

        _ledgerRepository =
            ledgerRepository;

        _transactionRepository =
            transactionRepository;

        _currentUserService =
            currentUserService;

        _paymentRequestRepository =
            paymentRequestRepository;
        
        _approvalPolicyService =
            approvalPolicyService;

        _approvalDecisionRepository =
            approvalDecisionRepository;
    }

    public async Task<CashMovementResponseDto>
        RecordReceipt(
            CreateCashReceiptDto dto)
    {
        ValidateReceipt(dto);

        var idempotencyKey =
            dto.IdempotencyKey.Trim();

        /*
         * A retried request returns the original
         * result instead of creating another receipt.
         */
        var existingTransaction =
            await _transactionRepository
                .GetByIdempotencyKey(
                    idempotencyKey);

        if (existingTransaction is not null)
        {
            if (!string.Equals(
                existingTransaction.TransactionType,
                TransactionTypes.CashReceipt,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    "The idempotency key has already " +
                    "been used for another operation.");
            }

            return MapResponse(
                existingTransaction);
        }

        var account =
            await _accountRepository
                .GetById(dto.AccountId);

        if (account is null)
        {
            throw new ResourceNotFoundException(
                "Account not found.");
        }

        if (!account.IsActive)
        {
            throw new ConflictException(
                "Cash receipts require an " +
                "active account.");
        }

        await _accountRepository
            .BeginTransaction();

        try
        {
            var completedAtUtc =
                DateTime.UtcNow;

            var transaction =
                new TreasuryTransaction
                {
                    Id = Guid.NewGuid(),

                    Reference =
                        TransactionReferenceGenerator
                            .Generate(),

                    TransactionType =
                        TransactionTypes
                            .CashReceipt,

                    Status =
                        TransactionStatuses
                            .Completed,

                    Amount =
                        dto.Amount,

                    Currency =
                        account.Currency
                            .Trim()
                            .ToUpperInvariant(),

                    Category =
                        dto.Category.Trim(),

                    CounterpartyName =
                        dto.CounterpartyName.Trim(),

                    ExternalReference =
                        NormalizeOptionalText(
                            dto.ExternalReference),

                    IdempotencyKey =
                        idempotencyKey,

                    Description =
                        dto.Description.Trim(),

                    SourceAccountId =
                        null,

                    DestinationAccountId =
                        account.Id,

                    InitiatedByUserId =
                        _currentUserService.UserId,

                    CompletedByUserId =
                        _currentUserService.UserId,

                    CreatedAtUtc =
                        completedAtUtc,

                    CompletedAtUtc =
                        completedAtUtc
                };

            account.Balance += dto.Amount;

            account.ConcurrencyToken =
                Guid.NewGuid();

            _accountRepository.Update(account);

            await _transactionRepository
                .Add(transaction);

            /*
             * A cash receipt increases the bank-account
             * asset and is recorded as a debit.
             */
            await _ledgerRepository.Add(
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),

                    TreasuryTransactionId =
                        transaction.Id,

                    AccountId =
                        account.Id,

                    Amount =
                        dto.Amount,

                    EntryType =
                        "Debit",

                    Description =
                        transaction.Description,

                    CreatedAt =
                        completedAtUtc
                });

            await _accountRepository
                .SaveChanges();

            await _accountRepository
                .CommitTransaction();

            return MapResponse(transaction);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "The account balance changed while " +
                "the receipt was processing. Refresh " +
                "and try again.");
        }
        catch
        {
            await _accountRepository
                .RollbackTransaction();

            throw;
        }
    }

    private static void ValidateReceipt(
        CreateCashReceiptDto dto)
    {
        if (dto.AccountId == Guid.Empty)
        {
            throw new ArgumentException(
                "Account is required.");
        }

        if (dto.Amount <= 0)
        {
            throw new ArgumentException(
                "Receipt amount must be " +
                "greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.CounterpartyName))
        {
            throw new ArgumentException(
                "Counterparty name is required.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.Category))
        {
            throw new ArgumentException(
                "Receipt category is required.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.IdempotencyKey))
        {
            throw new ArgumentException(
                "Idempotency key is required.");
        }

        if (dto.IdempotencyKey.Trim().Length > 100)
        {
            throw new ArgumentException(
                "Idempotency key cannot exceed " +
                "100 characters.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.Description))
        {
            throw new ArgumentException(
                "Description is required.");
        }
    }

    private static string?
        NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static CashMovementResponseDto
        MapResponse(
            TreasuryTransaction transaction)
    {
        return new CashMovementResponseDto
        {
            TransactionId =
                transaction.Id,

            TransactionReference =
                transaction.Reference,

            MovementType =
                transaction.TransactionType,

            Status =
                transaction.Status,

            AccountId =
                transaction.DestinationAccountId
                ?? throw new InvalidOperationException(
                    "Receipt account is missing."),

            Amount =
                transaction.Amount,

            Currency =
                transaction.Currency,

            Category =
                transaction.Category
                ?? string.Empty,

            CounterpartyName =
                transaction.CounterpartyName
                ?? string.Empty,

            ExternalReference =
                transaction.ExternalReference,

            Description =
                transaction.Description,

            CompletedAtUtc =
                transaction.CompletedAtUtc
                ?? transaction.CreatedAtUtc
        };
    }

    public async Task<CashPaymentResponseDto>
        RecordPayment(
            CreateCashPaymentDto dto)
    {
        ValidatePayment(dto);

        var idempotencyKey =
            dto.IdempotencyKey.Trim();

        var completedTransaction =
            await _transactionRepository
                .GetByIdempotencyKey(
                    idempotencyKey);

        if (completedTransaction is not null)
        {
            if (completedTransaction.TransactionType !=
                TransactionTypes.CashPayment)
            {
                throw new ConflictException(
                    "The idempotency key has already " +
                    "been used.");
            }

            return MapCompletedPayment(
                completedTransaction);
        }

        var existingRequest =
            await _paymentRequestRepository
                .GetByIdempotencyKey(
                    idempotencyKey);

        if (existingRequest is not null)
        {
            return MapPaymentRequest(
                existingRequest);
        }

        var account =
            await GetValidPaymentAccount(
                dto.AccountId,
                dto.Amount);

        var approvalRequirements =
            await _approvalPolicyService
                .GetRequirements(
                    ApprovalOperationTypes
                        .CashPayment,
                    account.Currency);
                    
        if (dto.Amount > approvalRequirements.ThresholdAmount)
        {   
            var createdAtUtc =
                DateTime.UtcNow;

            var request = new PaymentRequest
            {
                Id = Guid.NewGuid(),

                AccountId = account.Id,

                Amount = dto.Amount,

                Currency =
                    account.Currency
                        .Trim()
                        .ToUpperInvariant(),

                BeneficiaryName =
                    dto.BeneficiaryName.Trim(),

                Category =
                    dto.Category.Trim(),
                
                RequiredApprovalCount =
                    approvalRequirements
                        .RequiredApprovalCount,

                ApprovalCount = 0,

                ExternalReference =
                    NormalizeOptionalText(
                        dto.ExternalReference),

                IdempotencyKey =
                    idempotencyKey,

                Description =
                    dto.Description.Trim(),

                Status =
                    ApprovalStatus.Pending,

                RequestedByUserId =
                    _currentUserService.UserId,

                ConcurrencyToken =
                    Guid.NewGuid(),

                CreatedAtUtc = createdAtUtc,
                
                ExpiresAtUtc =
                    createdAtUtc.AddHours(
                        approvalRequirements
                            .PendingRequestExpiryHours),
            };
            
            ReservePaymentFunds(
                account,
                dto.Amount);

            try
            {
                await _paymentRequestRepository
                    .Add(request);

                await _paymentRequestRepository
                    .SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(
                    "The account balance changed while " +
                    "payment funds were being reserved.");
            }

            return MapPaymentRequest(request);
        }

        await _accountRepository
            .BeginTransaction();

        try
        {
            var transaction =
                await ExecutePayment(
                    account,
                    dto.Amount,
                    dto.BeneficiaryName,
                    dto.Category,
                    dto.ExternalReference,
                    idempotencyKey,
                    dto.Description,
                    paymentRequestId: null,
                    initiatedByUserId:
                        _currentUserService.UserId,
                    releaseReservation: false);

            await _accountRepository
                .SaveChanges();

            await _accountRepository
                .CommitTransaction();

            return MapCompletedPayment(
                transaction);
        }
        
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "The account balance changed while " +
                "the payment was processing. Refresh " +
                "and try again.");
        }
        catch
        {
            await _accountRepository
                .RollbackTransaction();

            throw;
        }
    }

    public async Task<List<CashPaymentResponseDto>>
        GetPendingPayments()
    {
        var requests =
            await _paymentRequestRepository
                .GetPending();

        return requests
            .Select(MapPaymentRequest)
            .ToList();
    }

    public async Task<CashPaymentResponseDto>
        ApprovePayment(Guid paymentRequestId)
    {
        await _accountRepository
            .BeginTransaction();

        try
        {
            var request =
                await GetPendingPayment(
                    paymentRequestId);

            EnsureDifferentReviewer(
                request.RequestedByUserId,
                "payment");

            var currentUserId =
                _currentUserService.UserId;

            var alreadyDecided =
                await _approvalDecisionRepository
                    .HasPaymentDecision(
                        request.Id,
                        currentUserId);

            if (alreadyDecided)
            {
                throw new ConflictException(
                    "You have already reviewed this " +
                    "payment request.");
            }

            await _approvalDecisionRepository.Add(
                new ApprovalDecision
                {
                    Id = Guid.NewGuid(),

                    PaymentRequestId =
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

            if (request.ApprovalCount <
                request.RequiredApprovalCount)
            {
                _paymentRequestRepository
                    .Update(request);

                await _accountRepository
                    .SaveChanges();

                await _accountRepository
                    .CommitTransaction();

                return MapPaymentRequest(request);
            }

            var account =
                await GetValidPaymentAccount(
                    request.AccountId,
                    request.Amount,
                    fundsAlreadyReserved: true);

            var transaction =
                await ExecutePayment(
                    account,
                    request.Amount,
                    request.BeneficiaryName,
                    request.Category,
                    request.ExternalReference,
                    request.IdempotencyKey,
                    request.Description,
                    paymentRequestId:
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

            _paymentRequestRepository
                .Update(request);

            await _accountRepository
                .SaveChanges();

            await _accountRepository
                .CommitTransaction();

            return MapCompletedPayment(
                transaction,
                request.ApprovalCount,
                request.RequiredApprovalCount);
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

    public async Task<CashPaymentResponseDto>
        RejectPayment(
            Guid paymentRequestId,
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
                await GetPendingPayment(
                    paymentRequestId);
            
            EnsureDifferentReviewer(
                request.RequestedByUserId,
                "payment");
            
            var currentUserId =
                _currentUserService.UserId;

            var alreadyDecided =
                await _approvalDecisionRepository
                    .HasPaymentDecision(
                        request.Id,
                        currentUserId);

            if (alreadyDecided)
            {
                throw new ConflictException(
                    "You have already reviewed this " +
                    "payment request.");
            }

            await _approvalDecisionRepository.Add(
                new ApprovalDecision
                {
                    Id = Guid.NewGuid(),

                    PaymentRequestId =
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
            
            var account =
                await _accountRepository
                    .GetById(request.AccountId);

            if (account is null)
            {
                throw new ResourceNotFoundException(
                    "Payment account not found.");
            }

            ReleasePaymentFunds(
                account,
                request.Amount);

            _paymentRequestRepository
                .Update(request);

            await _paymentRequestRepository
                .SaveChanges();

            await _accountRepository
                .CommitTransaction();
            
            await _accountRepository
                .SaveChanges();

            return MapPaymentRequest(request);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "This payment request was already " +
                "processed by another user.");
        }
        catch
        {
            await _accountRepository
                .RollbackTransaction();

            throw;
        }
    }

    private async Task<Account>
        GetValidPaymentAccount(
            Guid accountId,
            decimal amount,
            bool fundsAlreadyReserved = false)
    {
        var account =
            await _accountRepository
                .GetById(accountId);

        if (account is null)
        {
            throw new ResourceNotFoundException(
                "Payment account not found.");
        }

        if (!account.IsActive)
        {
            throw new ForbiddenOperationException(
                "Payments require an active account.");
        }

        if (fundsAlreadyReserved &&
            account.ReservedBalance < amount)
        {
            throw new ConflictException(
                "The expected payment reservation " +
                "was not found.");
        }

        var spendableBalance =
            account.AvailableBalance
            +
            (fundsAlreadyReserved
                ? amount
                : 0);

        if (spendableBalance < amount)
        {
            throw new BusinessRuleException(
                "Insufficient available funds.");
        }

        return account;
    }

    private async Task<PaymentRequest>
        GetPendingPayment(Guid requestId)
    {
        var request =
            await _paymentRequestRepository
                .GetById(requestId);

        if (request is null)
        {
            throw new ResourceNotFoundException(
                "Payment request not found.");
        }

        if (request.Status !=
            ApprovalStatus.Pending)
        {
            throw new ConflictException(
                "Payment request has already " +
                "been processed.");
        }

        PendingRequestExpiryGuard.EnsureNotExpired(
            request.ExpiresAtUtc,
            "payment");

        return request;
    }

    private async Task<TreasuryTransaction>
        ExecutePayment(
            Account account,
            decimal amount,
            string beneficiaryName,
            string category,
            string? externalReference,
            string idempotencyKey,
            string description,
            Guid? paymentRequestId,
            Guid initiatedByUserId,
            bool releaseReservation)
    {
        var completedAtUtc =
            DateTime.UtcNow;

        var transaction =
            new TreasuryTransaction
            {
                Id = Guid.NewGuid(),

                Reference =
                    TransactionReferenceGenerator
                        .Generate(),

                TransactionType =
                    TransactionTypes.CashPayment,

                Status =
                    TransactionStatuses.Completed,

                Amount = amount,

                Currency =
                    account.Currency
                        .Trim()
                        .ToUpperInvariant(),

                Category =
                    category.Trim(),

                CounterpartyName =
                    beneficiaryName.Trim(),

                ExternalReference =
                    NormalizeOptionalText(
                        externalReference),

                IdempotencyKey =
                    idempotencyKey.Trim(),

                Description =
                    description.Trim(),

                SourceAccountId =
                    account.Id,

                DestinationAccountId =
                    null,

                PaymentRequestId =
                    paymentRequestId,

                InitiatedByUserId =
                    initiatedByUserId,

                CompletedByUserId =
                    _currentUserService.UserId,

                CreatedAtUtc =
                    completedAtUtc,

                CompletedAtUtc =
                    completedAtUtc
            };
        
        if (releaseReservation)
        {
            if (account.ReservedBalance <
                amount)
            {
                throw new ConflictException(
                    "The expected payment reservation " +
                    "was not found.");
            }

            /*
            * Approval consumes the reservation and actual
            * balance together in one database update.
            */
            account.ReservedBalance -= amount;
        }

        account.Balance -= amount;

        if (account.ReservedBalance >
            account.Balance)
        {
            throw new ConflictException(
                "The remaining reservations exceed " +
                "the account balance.");
        }

        account.ConcurrencyToken =
            Guid.NewGuid();

        _accountRepository.Update(account);

        await _transactionRepository
            .Add(transaction);

        // A cash payment reduces the bank-account asset.
        await _ledgerRepository.Add(
            new LedgerEntry
            {
                Id = Guid.NewGuid(),

                TreasuryTransactionId =
                    transaction.Id,

                AccountId =
                    account.Id,

                Amount =
                    amount,

                EntryType =
                    "Credit",

                Description =
                    transaction.Description,

                CreatedAt =
                    completedAtUtc
            });

        return transaction;
    }

    private static void ValidatePayment(
        CreateCashPaymentDto dto)
    {
        if (dto.AccountId == Guid.Empty)
        {
            throw new ArgumentException(
                "Payment account is required.");
        }

        if (dto.Amount <= 0)
        {
            throw new ArgumentException(
                "Payment amount must be " +
                "greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.BeneficiaryName))
        {
            throw new ArgumentException(
                "Beneficiary name is required.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.Category))
        {
            throw new ArgumentException(
                "Payment category is required.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.IdempotencyKey))
        {
            throw new ArgumentException(
                "Idempotency key is required.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.Description))
        {
            throw new ArgumentException(
                "Description is required.");
        }
    }

    private static CashPaymentResponseDto
        MapPaymentRequest(
            PaymentRequest request)
    {
        return new CashPaymentResponseDto
        {
            PaymentRequestId =
                request.Id,

            TransactionId =
                null,

            TransactionReference =
                null,

            Status =
                request.Status,

            AccountId =
                request.AccountId,

            Amount =
                request.Amount,

            Currency =
                request.Currency,

            BeneficiaryName =
                request.BeneficiaryName,

            Category =
                request.Category,

            ExternalReference =
                request.ExternalReference,

            Description =
                request.Description,

            RejectionReason =
                request.RejectionReason,
            
            ApprovalCount =
                request.ApprovalCount,

            RequiredApprovalCount =
                request.RequiredApprovalCount,

            CreatedAtUtc =
                request.CreatedAtUtc,
            
            ExpiresAtUtc = request.ExpiresAtUtc
        };
    }

    private static CashPaymentResponseDto
        MapCompletedPayment(
            TreasuryTransaction transaction,
            int approvalCount = 0,
            int requiredApprovalCount = 0)
    {
        return new CashPaymentResponseDto
        {
            PaymentRequestId =
                transaction.PaymentRequestId,

            TransactionId =
                transaction.Id,

            TransactionReference =
                transaction.Reference,
            
            ApprovalCount =
                approvalCount,

            RequiredApprovalCount =
                requiredApprovalCount,

            Status =
                transaction.Status,

            AccountId =
                transaction.SourceAccountId
                ?? throw new InvalidOperationException(
                    "Payment account is missing."),

            Amount =
                transaction.Amount,

            Currency =
                transaction.Currency,

            BeneficiaryName =
                transaction.CounterpartyName
                ?? string.Empty,

            Category =
                transaction.Category
                ?? string.Empty,

            ExternalReference =
                transaction.ExternalReference,

            Description =
                transaction.Description,

            CreatedAtUtc =
                transaction.CompletedAtUtc
                ?? transaction.CreatedAtUtc
        };
    }
}