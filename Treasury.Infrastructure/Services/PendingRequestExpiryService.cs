using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Approvals;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class PendingRequestExpiryService
    : IPendingRequestExpiryService
{
    private const int BatchSize = 100;

    private readonly TreasuryDbContext _context;

    public PendingRequestExpiryService(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task<PendingRequestExpiryResultDto>
        ExpireDueRequests(
            CancellationToken cancellationToken =
                default)
    {
        var processedAtUtc =
            DateTime.UtcNow;

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            var transfers =
                await _context.TransferRequests
                    .Where(request =>
                        request.Status ==
                            ApprovalStatus.Pending &&
                        request.ExpiresAtUtc.HasValue &&
                        request.ExpiresAtUtc.Value <=
                            processedAtUtc)
                    .OrderBy(request =>
                        request.ExpiresAtUtc)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);

            var payments =
                await _context.PaymentRequests
                    .Where(request =>
                        request.Status ==
                            ApprovalStatus.Pending &&
                        request.ExpiresAtUtc.HasValue &&
                        request.ExpiresAtUtc.Value <=
                            processedAtUtc)
                    .OrderBy(request =>
                        request.ExpiresAtUtc)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);

            var reversals =
                await _context.ReversalRequests
                    .Where(request =>
                        request.Status ==
                            ApprovalStatus.Pending &&
                        request.ExpiresAtUtc.HasValue &&
                        request.ExpiresAtUtc.Value <=
                            processedAtUtc)
                    .OrderBy(request =>
                        request.ExpiresAtUtc)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);

            /*
             * Transfer and payment requests hold account
             * reservations that must be released.
             */
            var accountIds =
                transfers
                    .Select(request =>
                        request.FromAccountId)
                    .Concat(
                        payments.Select(request =>
                            request.AccountId))
                    .Distinct()
                    .ToList();

            var accounts =
                await _context.Accounts
                    .Where(account =>
                        accountIds.Contains(
                            account.Id))
                    .ToDictionaryAsync(
                        account => account.Id,
                        cancellationToken);

            foreach (var request in transfers)
            {
                if (!accounts.TryGetValue(
                    request.FromAccountId,
                    out var account))
                {
                    throw new ConflictException(
                        "The source account for an " +
                        "expired transfer was not found.");
                }

                ReleaseReservation(
                    account,
                    request.Amount,
                    "transfer");

                request.Status =
                    ApprovalStatus.Expired;

                request.ConcurrencyToken =
                    Guid.NewGuid();
            }

            foreach (var request in payments)
            {
                if (!accounts.TryGetValue(
                    request.AccountId,
                    out var account))
                {
                    throw new ConflictException(
                        "The account for an expired " +
                        "payment was not found.");
                }

                ReleaseReservation(
                    account,
                    request.Amount,
                    "payment");

                request.Status =
                    ApprovalStatus.Expired;

                request.ConcurrencyToken =
                    Guid.NewGuid();
            }

            foreach (var request in reversals)
            {
                // Reversals do not reserve funds.
                request.Status =
                    ApprovalStatus.Expired;

                request.ConcurrencyToken =
                    Guid.NewGuid();
            }

            await _context.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return new PendingRequestExpiryResultDto
            {
                ProcessedAtUtc =
                    processedAtUtc,

                ExpiredTransferCount =
                    transfers.Count,

                ExpiredPaymentCount =
                    payments.Count,

                ExpiredReversalCount =
                    reversals.Count
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _context.ChangeTracker.Clear();

            throw new ConflictException(
                "A pending request or account changed " +
                "while expiration was processing.");
        }
        catch
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _context.ChangeTracker.Clear();

            throw;
        }
    }

    private static void ReleaseReservation(
        Account account,
        decimal amount,
        string requestType)
    {
        if (account.ReservedBalance < amount)
        {
            throw new ConflictException(
                $"The expired {requestType} does not " +
                "have the expected reservation.");
        }

        /*
         * Expiration releases reserved funds but does
         * not change the account's actual balance.
         */
        account.ReservedBalance -= amount;

        account.ConcurrencyToken =
            Guid.NewGuid();
    }
}