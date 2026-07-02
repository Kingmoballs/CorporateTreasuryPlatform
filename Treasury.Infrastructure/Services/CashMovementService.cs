using Treasury.Application.DTOs.CashMovements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Common;
using Treasury.Shared.Constants;
using Microsoft.EntityFrameworkCore;

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
    
    private const decimal ApprovalThreshold =
        10000000m;

    private readonly IPaymentRequestRepository
        _paymentRequestRepository;

    public CashMovementService(
        IAccountRepository accountRepository,
        ILedgerRepository ledgerRepository,
        ITreasuryTransactionRepository
            transactionRepository,
        ICurrentUserService currentUserService,
        IPaymentRequestRepository paymentRequestRepository)
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
                throw new Exception(
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
            throw new Exception(
                "Account not found.");
        }

        if (!account.IsActive)
        {
            throw new Exception(
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
                throw new Exception(
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

        if (dto.Amount > ApprovalThreshold)
        {
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

                CreatedAtUtc =
                    DateTime.UtcNow
            };

            await _paymentRequestRepository
                .Add(request);

            await _paymentRequestRepository
                .SaveChanges();

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
                        _currentUserService.UserId);

            await _accountRepository
                .SaveChanges();

            await _accountRepository
                .CommitTransaction();

            return MapCompletedPayment(
                transaction);
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

            var account =
                await GetValidPaymentAccount(
                    request.AccountId,
                    request.Amount);

            var transaction =
                await ExecutePayment(
                    account,
                    request.Amount,
                    request.BeneficiaryName,
                    request.Category,
                    request.ExternalReference,
                    request.IdempotencyKey,
                    request.Description,
                    request.Id,
                    request.RequestedByUserId);

            request.Status =
                ApprovalStatus.Approved;

            request.ReviewedByUserId =
                _currentUserService.UserId;

            request.ReviewedAtUtc =
                DateTime.UtcNow;

            request.ConcurrencyToken =
                Guid.NewGuid();

            _paymentRequestRepository
                .Update(request);

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

            throw new Exception(
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

            _paymentRequestRepository
                .Update(request);

            await _paymentRequestRepository
                .SaveChanges();

            await _accountRepository
                .CommitTransaction();

            return MapPaymentRequest(request);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new Exception(
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
            decimal amount)
    {
        var account =
            await _accountRepository
                .GetById(accountId);

        if (account is null)
        {
            throw new Exception(
                "Payment account not found.");
        }

        if (!account.IsActive)
        {
            throw new Exception(
                "Payments require an active account.");
        }

        if (account.Balance < amount)
        {
            throw new Exception(
                "Insufficient funds.");
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
            throw new Exception(
                "Payment request not found.");
        }

        if (request.Status !=
            ApprovalStatus.Pending)
        {
            throw new Exception(
                "Payment request has already " +
                "been processed.");
        }

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
            Guid initiatedByUserId)
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

        account.Balance -= amount;

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

            CreatedAtUtc =
                request.CreatedAtUtc
        };
    }

    private static CashPaymentResponseDto
        MapCompletedPayment(
            TreasuryTransaction transaction)
    {
        return new CashPaymentResponseDto
        {
            PaymentRequestId =
                transaction.PaymentRequestId,

            TransactionId =
                transaction.Id,

            TransactionReference =
                transaction.Reference,

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