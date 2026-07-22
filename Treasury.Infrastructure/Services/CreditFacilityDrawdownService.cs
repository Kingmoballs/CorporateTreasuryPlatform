using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.CreditFacilityDrawdowns;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Common;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class CreditFacilityDrawdownService
    : ICreditFacilityDrawdownService
{
    private readonly ICreditFacilityDrawdownRepository
        _drawdownRepository;

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

    public CreditFacilityDrawdownService(
        ICreditFacilityDrawdownRepository drawdownRepository,
        ICreditFacilityRepository facilityRepository,
        IAccountRepository accountRepository,
        ITreasuryTransactionRepository transactionRepository,
        ILedgerRepository ledgerRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _drawdownRepository =
            drawdownRepository;

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

    public async Task<CreditFacilityDrawdownResponseDto>
        Execute(
            Guid creditFacilityId,
            CreateCreditFacilityDrawdownDto dto)
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
             * Return the original completed drawdown
             * when the client retries with the same key.
             */
            var existingDrawdown =
                await _drawdownRepository
                    .GetByIdempotencyKey(
                        idempotencyKey);

            if (existingDrawdown is not null)
            {
                if (existingDrawdown.CreditFacilityId !=
                    creditFacilityId)
                {
                    throw new ConflictException(
                        "The idempotency key has already " +
                        "been used for another facility.");
                }

                await _accountRepository
                    .CommitTransaction();

                return Map(existingDrawdown);
            }

            /*
             * Treasury transaction keys are globally unique.
             * This prevents a key used by another cash
             * operation from being reused for a drawdown.
             */
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

            var beforePrincipal =
                facility.OutstandingPrincipalAmount;

            var afterPrincipal =
                beforePrincipal + dto.Amount;

            var now =
                DateTime.UtcNow;

            var description =
                string.IsNullOrWhiteSpace(
                    dto.Description)
                    ? $"Drawdown on credit facility " +
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
                            .CreditFacilityDrawdown,

                    Status =
                        TransactionStatuses.Completed,

                    Amount =
                        dto.Amount,

                    Currency =
                        facility.Currency,

                    Description =
                        description,

                    SourceAccountId =
                        null,

                    DestinationAccountId =
                        settlementAccount.Id,

                    InitiatedByUserId =
                        _currentUserService.UserId,

                    CompletedByUserId =
                        _currentUserService.UserId,

                    Category =
                        "Credit Facility Drawdown",

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

            var drawdown =
                new CreditFacilityDrawdown
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

                    Currency =
                        facility.Currency,

                    OutstandingPrincipalBefore =
                        beforePrincipal,

                    OutstandingPrincipalAfter =
                        afterPrincipal,

                    Status =
                        CreditFacilityDrawdownStatuses
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

                    DrawdownDateUtc =
                        now,

                    CreatedAtUtc =
                        now
                };

            /*
             * Drawing down a facility increases both the
             * debt principal and the bank-account cash.
             */
            facility.OutstandingPrincipalAmount =
                afterPrincipal;

            facility.UpdatedByUserId =
                _currentUserService.UserId;

            facility.UpdatedAtUtc =
                now;

            facility.ConcurrencyToken =
                Guid.NewGuid();

            settlementAccount.Balance +=
                dto.Amount;

            settlementAccount.ConcurrencyToken =
                Guid.NewGuid();

            _facilityRepository.Update(facility);

            _accountRepository.Update(
                settlementAccount);

            await _transactionRepository.Add(
                treasuryTransaction);

            await _drawdownRepository.Add(
                drawdown);

            /*
             * Cash entering the bank account increases
             * the cash asset, so the operational ledger
             * entry is a Debit.
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
                        "Debit",

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
             * All tracked changes use the same DbContext,
             * so this SaveChanges call persists them as
             * one unit inside the active transaction.
             */
            await _accountRepository.SaveChanges();

            await _auditLogService.Record(
                new CreateAuditLogDto
                {
                    Action =
                        AuditActionTypes.DrawnDown,

                    EntityType =
                        AuditEntityTypes
                            .CreditFacilityDrawdown,

                    EntityId =
                        drawdown.Id,

                    EntityReference =
                        drawdown.Reference,

                    Summary =
                        $"Credit facility " +
                        $"{facility.Reference} was drawn " +
                        $"down by {dto.Amount:N2} " +
                        $"{facility.Currency}.",

                    AfterValues =
                        new
                        {
                            drawdown.Id,
                            drawdown.Reference,
                            drawdown.CreditFacilityId,
                            drawdown.Amount,
                            drawdown.Currency,
                            drawdown
                                .OutstandingPrincipalBefore,
                            drawdown
                                .OutstandingPrincipalAfter,
                            drawdown.Status,
                            drawdown.TreasuryTransactionId,
                            drawdown.DrawdownDateUtc
                        },

                    Metadata =
                        new
                        {
                            Module =
                                "Credit Facility Drawdowns",

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

            return Map(drawdown);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "The facility or settlement-account balance " +
                "changed while the drawdown was processing. " +
                "Refresh and try again.");
        }
        catch (DbUpdateException)
        {
            await _accountRepository
                .RollbackTransaction();

            throw new ConflictException(
                "The drawdown could not be saved. The " +
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

    public async Task<CreditFacilityDrawdownResponseDto>
        GetById(Guid id)
    {
        var drawdown =
            await _drawdownRepository.GetById(id);

        if (drawdown is null)
        {
            throw new ResourceNotFoundException(
                "Credit facility drawdown was not found.");
        }

        return Map(drawdown);
    }

    public async Task<
        PagedCreditFacilityDrawdownResponseDto>
        Search(
            Guid creditFacilityId,
            CreditFacilityDrawdownQueryDto query)
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

        /*
         * Distinguish an empty result from an invalid
         * facility identifier.
         */
        var facility =
            await _facilityRepository
                .GetById(creditFacilityId);

        if (facility is null)
        {
            throw new ResourceNotFoundException(
                "Credit facility was not found.");
        }

        var result =
            await _drawdownRepository.Search(
                creditFacilityId,
                query);

        return new PagedCreditFacilityDrawdownResponseDto
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
        CreateCreditFacilityDrawdownDto dto)
    {
        if (creditFacilityId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Credit facility ID is required.");
        }

        if (dto.Amount <= 0)
        {
            throw new BusinessRuleException(
                "Drawdown amount must be greater than zero.");
        }

        if (dto.Amount >
            999_999_999_999_999.99m)
        {
            throw new BusinessRuleException(
                "Drawdown amount is too large.");
        }
    }

    private static void ValidateFacility(
        CreditFacility facility,
        decimal amount)
    {
        if (facility.Status !=
            CreditFacilityStatuses.Active)
        {
            throw new ConflictException(
                "Only an active credit facility can " +
                "be drawn down.");
        }

        if (facility.StartDateUtc.Date >
            DateTime.UtcNow.Date)
        {
            throw new BusinessRuleException(
                "The facility start date has not been reached.");
        }

        if (facility.MaturityDateUtc.Date <=
            DateTime.UtcNow.Date)
        {
            throw new BusinessRuleException(
                "The credit facility has matured.");
        }

        if (facility.SettlementAccount is null)
        {
            throw new ResourceNotFoundException(
                "Facility settlement account was not loaded.");
        }

        if (!facility.SettlementAccount.IsActive)
        {
            throw new ConflictException(
                "The facility settlement account is inactive.");
        }

        if (!string.Equals(
                facility.Currency,
                facility.SettlementAccount.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "The facility currency does not match " +
                "the settlement-account currency.");
        }

        var availableAmount =
            facility.ApprovedLimitAmount -
            facility.OutstandingPrincipalAmount;

        if (availableAmount < 0)
        {
            throw new ConflictException(
                "The facility outstanding principal " +
                "already exceeds its approved limit.");
        }

        if (amount > availableAmount)
        {
            throw new BusinessRuleException(
                $"Drawdown amount exceeds the available " +
                $"facility amount of {availableAmount:N2} " +
                $"{facility.Currency}.");
        }
    }

    private async Task<string> GenerateReference()
    {
        for (var attempt = 0;
             attempt < 10;
             attempt++)
        {
            var reference =
                $"DRW-{DateTime.UtcNow:yyyyMMdd}-" +
                Guid.NewGuid()
                    .ToString("N")[..8]
                    .ToUpperInvariant();

            if (!await _drawdownRepository
                    .ReferenceExists(reference))
            {
                return reference;
            }
        }

        throw new ConflictException(
            "Unable to generate a unique drawdown reference.");
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

    private static CreditFacilityDrawdownResponseDto Map(
        CreditFacilityDrawdown drawdown)
    {
        var approvedLimit =
            drawdown.CreditFacility
                ?.ApprovedLimitAmount ?? 0m;

        return new CreditFacilityDrawdownResponseDto
        {
            Id =
                drawdown.Id,

            Reference =
                drawdown.Reference,

            CreditFacilityId =
                drawdown.CreditFacilityId,

            CreditFacilityReference =
                drawdown.CreditFacility?.Reference
                ?? string.Empty,

            FacilityName =
                drawdown.CreditFacility?.FacilityName
                ?? string.Empty,

            LenderName =
                drawdown.CreditFacility?.LenderName
                ?? string.Empty,

            SettlementAccountId =
                drawdown.SettlementAccountId,

            SettlementAccountName =
                drawdown.SettlementAccount?.Name
                ?? string.Empty,

            SettlementAccountNumber =
                drawdown.SettlementAccount?.AccountNumber
                ?? string.Empty,

            Amount =
                drawdown.Amount,

            Currency =
                drawdown.Currency,

            ApprovedLimitAmount =
                approvedLimit,

            OutstandingPrincipalBefore =
                drawdown.OutstandingPrincipalBefore,

            OutstandingPrincipalAfter =
                drawdown.OutstandingPrincipalAfter,

            AvailableAmountAfter =
                approvedLimit -
                drawdown.OutstandingPrincipalAfter,

            Status =
                drawdown.Status,

            ExternalReference =
                drawdown.ExternalReference,

            IdempotencyKey =
                drawdown.IdempotencyKey,

            Description =
                drawdown.Description,

            TreasuryTransactionId =
                drawdown.TreasuryTransactionId,

            TreasuryTransactionReference =
                drawdown.TreasuryTransaction?.Reference
                ?? string.Empty,

            InitiatedByUserId =
                drawdown.InitiatedByUserId,

            DrawdownDateUtc =
                drawdown.DrawdownDateUtc,

            CreatedAtUtc =
                drawdown.CreatedAtUtc
        };
    }
}