using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.CreditFacilityRepayments;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Common;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class CreditFacilityRepaymentService
    : ICreditFacilityRepaymentService
{
    private readonly ICreditFacilityRepaymentRepository
        _repaymentRepository;

    private readonly ICreditFacilityRepository
        _facilityRepository;

    private readonly IAccountRepository
        _accountRepository;

    private readonly ITreasuryTransactionRepository
        _transactionRepository;

    private readonly ILedgerRepository
        _ledgerRepository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuditLogService
        _auditLogService;

    public CreditFacilityRepaymentService(
        ICreditFacilityRepaymentRepository repaymentRepository,
        ICreditFacilityRepository facilityRepository,
        IAccountRepository accountRepository,
        ITreasuryTransactionRepository transactionRepository,
        ILedgerRepository ledgerRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _repaymentRepository =
            repaymentRepository;

        _facilityRepository =
            facilityRepository;

        _accountRepository =
            accountRepository;

        _transactionRepository =
            transactionRepository;

        _ledgerRepository =
            ledgerRepository;

        _currentUserService =
            currentUserService;

        _auditLogService =
            auditLogService;
    }

    public async Task<CreditFacilityRepaymentResponseDto>
        Execute(
            Guid creditFacilityId,
            CreateCreditFacilityRepaymentDto dto)
    {
        ValidateRequest(
            creditFacilityId,
            dto);

        var idempotencyKey =
            NormalizeRequiredText(
                dto.IdempotencyKey,
                "Idempotency key",
                100);

        await _accountRepository.BeginTransaction();

        try
        {
            /*
             * A repeated request with the same key returns
             * the original repayment without debiting the
             * account again.
             */
            var existingRepayment =
                await _repaymentRepository
                    .GetByIdempotencyKey(
                        idempotencyKey);

            if (existingRepayment is not null)
            {
                if (existingRepayment.CreditFacilityId !=
                    creditFacilityId)
                {
                    throw new ConflictException(
                        "The idempotency key has already " +
                        "been used for another facility.");
                }

                await _accountRepository
                    .CommitTransaction();

                return Map(existingRepayment);
            }

            var existingTransaction =
                await _transactionRepository
                    .GetByIdempotencyKey(
                        idempotencyKey);

            if (existingTransaction is not null)
            {
                throw new ConflictException(
                    "The idempotency key has already been used.");
            }

            var facility =
                await _facilityRepository
                    .GetById(creditFacilityId);

            if (facility is null)
            {
                throw new ResourceNotFoundException(
                    "Credit facility was not found.");
            }

            ValidateFacility(
                facility,
                dto.Amount);

            var settlementAccount =
                facility.SettlementAccount
                ?? throw new ResourceNotFoundException(
                    "Facility settlement account was not loaded.");

            var principalBefore =
                facility.OutstandingPrincipalAmount;

            var accruedInterestBefore =
                facility.AccruedInterestAmount;

            /*
             * Standard debt-payment waterfall:
             * accrued interest is settled before principal.
             */
            var interestAmount =
                Math.Min(
                    dto.Amount,
                    accruedInterestBefore);

            var principalAmount =
                dto.Amount - interestAmount;

            if (principalAmount >
                principalBefore)
            {
                throw new BusinessRuleException(
                    "The repayment principal component " +
                    "exceeds outstanding principal.");
            }

            var principalAfter =
                principalBefore - principalAmount;

            var accruedInterestAfter =
                accruedInterestBefore - interestAmount;

            var now =
                DateTime.UtcNow;

            var description =
                string.IsNullOrWhiteSpace(
                    dto.Description)
                    ? $"Repayment on credit facility " +
                      $"{facility.Reference}."
                    : NormalizeRequiredText(
                        dto.Description,
                        "Description",
                        500);

            var externalReference =
                NormalizeOptionalText(
                    dto.ExternalReference,
                    100);

            var treasuryTransaction =
                new TreasuryTransaction
                {
                    Id =
                        Guid.NewGuid(),

                    Reference =
                        TransactionReferenceGenerator
                            .Generate(),

                    TransactionType =
                        TransactionTypes
                            .CreditFacilityRepayment,

                    Status =
                        TransactionStatuses.Completed,

                    Amount =
                        dto.Amount,

                    Currency =
                        facility.Currency,

                    Description =
                        description,

                    SourceAccountId =
                        settlementAccount.Id,

                    DestinationAccountId =
                        null,

                    InitiatedByUserId =
                        _currentUserService.UserId,

                    CompletedByUserId =
                        _currentUserService.UserId,

                    Category =
                        "Credit Facility Repayment",

                    CounterpartyName =
                        facility.LenderName,

                    ExternalReference =
                        externalReference,

                    IdempotencyKey =
                        idempotencyKey,

                    CreatedAtUtc =
                        now,

                    CompletedAtUtc =
                        now
                };

            var repayment =
                new CreditFacilityRepayment
                {
                    Id =
                        Guid.NewGuid(),

                    Reference =
                        await GenerateReference(),

                    CreditFacilityId =
                        facility.Id,

                    CreditFacility =
                        facility,

                    SettlementAccountId =
                        settlementAccount.Id,

                    SettlementAccount =
                        settlementAccount,

                    Amount =
                        dto.Amount,

                    PrincipalAmount =
                        principalAmount,

                    InterestAmount =
                        interestAmount,

                    Currency =
                        facility.Currency,

                    OutstandingPrincipalBefore =
                        principalBefore,

                    OutstandingPrincipalAfter =
                        principalAfter,

                    AccruedInterestBefore =
                        accruedInterestBefore,

                    AccruedInterestAfter =
                        accruedInterestAfter,

                    Status =
                        CreditFacilityRepaymentStatuses
                            .Completed,

                    ExternalReference =
                        externalReference,

                    IdempotencyKey =
                        idempotencyKey,

                    Description =
                        description,

                    TreasuryTransactionId =
                        treasuryTransaction.Id,

                    TreasuryTransaction =
                        treasuryTransaction,

                    InitiatedByUserId =
                        _currentUserService.UserId,

                    RepaymentDateUtc =
                        now,

                    CreatedAtUtc =
                        now
                };

            facility.OutstandingPrincipalAmount =
                principalAfter;

            facility.AccruedInterestAmount =
                accruedInterestAfter;

            facility.UpdatedByUserId =
                _currentUserService.UserId;

            facility.UpdatedAtUtc =
                now;

            facility.ConcurrencyToken =
                Guid.NewGuid();

            /*
             * Repayment sends money out of the settlement
             * account to the lender.
             */
            settlementAccount.Balance -=
                dto.Amount;

            if (settlementAccount.ReservedBalance >
                settlementAccount.Balance)
            {
                throw new ConflictException(
                    "The repayment would leave the account " +
                    "unable to cover its reserved funds.");
            }

            settlementAccount.ConcurrencyToken =
                Guid.NewGuid();

            _facilityRepository.Update(facility);

            _accountRepository.Update(
                settlementAccount);

            await _transactionRepository.Add(
                treasuryTransaction);

            await _repaymentRepository.Add(
                repayment);

            /*
             * Cash leaving the bank account reduces the
             * cash asset, so the ledger entry is a Credit.
             */
            await _ledgerRepository.Add(
                new LedgerEntry
                {
                    Id =
                        Guid.NewGuid(),

                    AccountId =
                        settlementAccount.Id,

                    Account =
                        settlementAccount,

                    Amount =
                        dto.Amount,

                    EntryType =
                        "Credit",

                    Description =
                        description,

                    TreasuryTransactionId =
                        treasuryTransaction.Id,

                    TreasuryTransaction =
                        treasuryTransaction,

                    CreatedAt =
                        now
                });

            /*
             * The repayment, facility, account,
             * transaction and ledger entry share the same
             * DbContext and database transaction.
             */
            await _accountRepository.SaveChanges();

            await _auditLogService.Record(
                new CreateAuditLogDto
                {
                    Action =
                        AuditActionTypes.Repaid,

                    EntityType =
                        AuditEntityTypes
                            .CreditFacilityRepayment,

                    EntityId =
                        repayment.Id,

                    EntityReference =
                        repayment.Reference,

                    Summary =
                        $"Credit facility " +
                        $"{facility.Reference} was repaid " +
                        $"by {dto.Amount:N2} " +
                        $"{facility.Currency}.",

                    AfterValues =
                        new
                        {
                            repayment.Id,
                            repayment.Reference,
                            repayment.CreditFacilityId,
                            repayment.Amount,
                            repayment.PrincipalAmount,
                            repayment.InterestAmount,
                            repayment.Currency,
                            repayment
                                .OutstandingPrincipalBefore,
                            repayment
                                .OutstandingPrincipalAfter,
                            repayment
                                .AccruedInterestBefore,
                            repayment
                                .AccruedInterestAfter,
                            repayment.TreasuryTransactionId,
                            repayment.RepaymentDateUtc
                        },

                    Metadata =
                        new
                        {
                            Module =
                                "Credit Facility Repayments",

                            FacilityReference =
                                facility.Reference,

                            SettlementAccountId =
                                settlementAccount.Id,

                            TransactionReference =
                                treasuryTransaction.Reference,

                            AccountBalanceAfter =
                                settlementAccount.Balance
                        }
                });

            await _accountRepository
                .CommitTransaction();

            return Map(repayment);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "The facility or settlement-account balance " +
                "changed while repayment was processing. " +
                "Refresh and try again.");
        }
        catch (DbUpdateException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "The repayment could not be saved. The " +
                "idempotency key or reference may already " +
                "be in use.");
        }
        catch
        {
            await _accountRepository
                .RollbackTransaction();

            throw;
        }
    }

    public async Task<CreditFacilityRepaymentResponseDto>
        GetById(Guid id)
    {
        var repayment =
            await _repaymentRepository.GetById(id);

        if (repayment is null)
        {
            throw new ResourceNotFoundException(
                "Credit facility repayment was not found.");
        }

        return Map(repayment);
    }

    public async Task<
        PagedCreditFacilityRepaymentResponseDto>
        Search(
            Guid creditFacilityId,
            CreditFacilityRepaymentQueryDto query)
    {
        if (creditFacilityId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Credit facility ID is required.");
        }

        query.Page =
            query.Page < 1 ? 1 : query.Page;

        query.PageSize =
            query.PageSize < 1
                ? 50
                : Math.Min(query.PageSize, 100);

        if (query.FromUtc.HasValue)
        {
            query.FromUtc =
                NormalizeUtc(query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            query.ToUtc =
                NormalizeUtc(query.ToUtc.Value);
        }

        if (query.FromUtc.HasValue &&
            query.ToUtc.HasValue &&
            query.FromUtc.Value >=
                query.ToUtc.Value)
        {
            throw new BusinessRuleException(
                "FromUtc must be earlier than ToUtc.");
        }

        var facility =
            await _facilityRepository
                .GetById(creditFacilityId);

        if (facility is null)
        {
            throw new ResourceNotFoundException(
                "Credit facility was not found.");
        }

        var result =
            await _repaymentRepository.Search(
                creditFacilityId,
                query);

        return new PagedCreditFacilityRepaymentResponseDto
        {
            Items =
                result.Items.Select(Map).ToList(),

            Page =
                query.Page,

            PageSize =
                query.PageSize,

            TotalCount =
                result.TotalCount,

            TotalPages =
                (int)Math.Ceiling(
                    result.TotalCount /
                    (double)query.PageSize)
        };
    }

    private static void ValidateRequest(
        Guid creditFacilityId,
        CreateCreditFacilityRepaymentDto dto)
    {
        if (creditFacilityId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Credit facility ID is required.");
        }

        if (dto.Amount <= 0)
        {
            throw new BusinessRuleException(
                "Repayment amount must be greater than zero.");
        }

        /*
         * All persisted monetary values have scale 2.
         */
        if (decimal.Round(dto.Amount, 2) !=
            dto.Amount)
        {
            throw new BusinessRuleException(
                "Repayment amount cannot contain more " +
                "than two decimal places.");
        }

        if (dto.Amount >
            999_999_999_999_999.99m)
        {
            throw new BusinessRuleException(
                "Repayment amount is too large.");
        }
    }

    private static void ValidateFacility(
        CreditFacility facility,
        decimal amount)
    {
        /*
         * Suspended and matured facilities cannot accept
         * new drawdowns, but outstanding debt can still
         * be repaid.
         */
        var repaymentAllowed =
            facility.Status ==
                CreditFacilityStatuses.Active ||
            facility.Status ==
                CreditFacilityStatuses.Suspended ||
            facility.Status ==
                CreditFacilityStatuses.Matured;

        if (!repaymentAllowed)
        {
            throw new ConflictException(
                "The facility is not in a state that " +
                "allows repayment.");
        }

        var settlementAccount =
            facility.SettlementAccount;

        if (settlementAccount is null)
        {
            throw new ResourceNotFoundException(
                "Facility settlement account was not loaded.");
        }

        if (!settlementAccount.IsActive)
        {
            throw new ConflictException(
                "The facility settlement account is inactive.");
        }

        if (!string.Equals(
                facility.Currency,
                settlementAccount.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "The facility currency does not match " +
                "the settlement-account currency.");
        }

        var totalOutstanding =
            facility.OutstandingPrincipalAmount +
            facility.AccruedInterestAmount;

        if (totalOutstanding <= 0)
        {
            throw new ConflictException(
                "The credit facility has no outstanding " +
                "principal or interest to repay.");
        }

        if (amount > totalOutstanding)
        {
            throw new BusinessRuleException(
                $"Repayment amount exceeds total outstanding " +
                $"debt of {totalOutstanding:N2} " +
                $"{facility.Currency}.");
        }

        if (settlementAccount.AvailableBalance <
            amount)
        {
            throw new BusinessRuleException(
                $"Insufficient available balance in the " +
                $"settlement account. Available balance is " +
                $"{settlementAccount.AvailableBalance:N2} " +
                $"{settlementAccount.Currency}.");
        }
    }

    private async Task<string> GenerateReference()
    {
        for (var attempt = 0;
             attempt < 10;
             attempt++)
        {
            var reference =
                $"RPM-{DateTime.UtcNow:yyyyMMdd}-" +
                Guid.NewGuid()
                    .ToString("N")[..8]
                    .ToUpperInvariant();

            if (!await _repaymentRepository
                    .ReferenceExists(reference))
            {
                return reference;
            }
        }

        throw new ConflictException(
            "Unable to generate a unique repayment reference.");
    }

    private static string NormalizeRequiredText(
        string value,
        string fieldName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                $"{fieldName} is required.");
        }

        var normalized =
            value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new BusinessRuleException(
                $"{fieldName} cannot exceed " +
                $"{maximumLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized =
            value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new BusinessRuleException(
                $"Value cannot exceed " +
                $"{maximumLength} characters.");
        }

        return normalized;
    }

    private static DateTime NormalizeUtc(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc =>
                value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc)
        };
    }

    private static CreditFacilityRepaymentResponseDto Map(
        CreditFacilityRepayment repayment)
    {
        return new CreditFacilityRepaymentResponseDto
        {
            Id =
                repayment.Id,

            Reference =
                repayment.Reference,

            CreditFacilityId =
                repayment.CreditFacilityId,

            CreditFacilityReference =
                repayment.CreditFacility?.Reference
                ?? string.Empty,

            FacilityName =
                repayment.CreditFacility?.FacilityName
                ?? string.Empty,

            LenderName =
                repayment.CreditFacility?.LenderName
                ?? string.Empty,

            SettlementAccountId =
                repayment.SettlementAccountId,

            SettlementAccountName =
                repayment.SettlementAccount?.Name
                ?? string.Empty,

            SettlementAccountNumber =
                repayment.SettlementAccount?.AccountNumber
                ?? string.Empty,

            Amount =
                repayment.Amount,

            PrincipalAmount =
                repayment.PrincipalAmount,

            InterestAmount =
                repayment.InterestAmount,

            Currency =
                repayment.Currency,

            OutstandingPrincipalBefore =
                repayment.OutstandingPrincipalBefore,

            OutstandingPrincipalAfter =
                repayment.OutstandingPrincipalAfter,

            AccruedInterestBefore =
                repayment.AccruedInterestBefore,

            AccruedInterestAfter =
                repayment.AccruedInterestAfter,

            TotalOutstandingAfter =
                repayment.OutstandingPrincipalAfter +
                repayment.AccruedInterestAfter,

            Status =
                repayment.Status,

            ExternalReference =
                repayment.ExternalReference,

            IdempotencyKey =
                repayment.IdempotencyKey,

            Description =
                repayment.Description,

            TreasuryTransactionId =
                repayment.TreasuryTransactionId,

            TreasuryTransactionReference =
                repayment.TreasuryTransaction?.Reference
                ?? string.Empty,

            InitiatedByUserId =
                repayment.InitiatedByUserId,

            RepaymentDateUtc =
                repayment.RepaymentDateUtc,

            CreatedAtUtc =
                repayment.CreatedAtUtc
        };
    }
}