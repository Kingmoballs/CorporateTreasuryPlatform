using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.TreasuryAlerts;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Repositories;

public class TreasuryAlertRepository : ITreasuryAlertRepository
{
    private readonly TreasuryDbContext _context;

    public TreasuryAlertRepository(TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(TreasuryAlert alert)
    {
        await _context.TreasuryAlerts.AddAsync(alert);
    }

    public async Task<TreasuryAlert?> GetById(Guid id)
    {
        return await _context.TreasuryAlerts
            .Include(alert => alert.Account)
            .FirstOrDefaultAsync(alert => alert.Id == id);
    }

    public async Task<(IReadOnlyList<TreasuryAlert> Items, int TotalCount)> Search(
        TreasuryAlertQueryDto query)
    {
        var page =
            query.Page < 1 ? 1 : query.Page;

        var pageSize =
            query.PageSize < 1 ? 50 : Math.Min(query.PageSize, 100);

        var alerts =
            _context.TreasuryAlerts
                .AsNoTracking()
                .Include(alert => alert.Account)
                .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            alerts =
                alerts.Where(alert =>
                    alert.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.AlertType))
        {
            alerts =
                alerts.Where(alert =>
                    alert.AlertType == query.AlertType);
        }

        if (!string.IsNullOrWhiteSpace(query.Severity))
        {
            alerts =
                alerts.Where(alert =>
                    alert.Severity == query.Severity);
        }

        if (query.AccountId.HasValue)
        {
            alerts =
                alerts.Where(alert =>
                    alert.AccountId == query.AccountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            alerts =
                alerts.Where(alert =>
                    alert.Currency == query.Currency);
        }

        if (query.FromUtc.HasValue)
        {
            alerts =
                alerts.Where(alert =>
                    alert.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            alerts =
                alerts.Where(alert =>
                    alert.CreatedAtUtc <= query.ToUtc.Value);
        }

        var totalCount =
            await alerts.CountAsync();

        var items =
            await alerts
                .OrderByDescending(alert =>
                    alert.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        return (items, totalCount);
    }

    public void Update(TreasuryAlert alert)
    {
        _context.TreasuryAlerts.Update(alert);
    }

    public async Task<bool> OpenAlertExists(
        string alertType,
        string? sourceEntityType,
        Guid? sourceEntityId,
        string? sourceReference)
    {
        return await _context.TreasuryAlerts
            .AsNoTracking()
            .AnyAsync(alert =>
                alert.Status == "Open" &&
                alert.AlertType == alertType &&
                alert.SourceEntityType == sourceEntityType &&
                alert.SourceEntityId == sourceEntityId &&
                alert.SourceReference == sourceReference);
    }

    public async Task<IReadOnlyList<TreasuryAlert>> GetForSummary(
        TreasuryAlertSummaryQueryDto query)
    {
        var alerts =
            _context.TreasuryAlerts
                .AsNoTracking()
                .Include(alert =>
                    alert.Account)
                .AsQueryable();

        if (query.AccountId.HasValue)
        {
            alerts =
                alerts.Where(alert =>
                    alert.AccountId == query.AccountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            alerts =
                alerts.Where(alert =>
                    alert.Currency == query.Currency);
        }

        if (query.FromUtc.HasValue)
        {
            alerts =
                alerts.Where(alert =>
                    alert.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            alerts =
                alerts.Where(alert =>
                    alert.CreatedAtUtc <= query.ToUtc.Value);
        }

        return await alerts
            .OrderByDescending(alert =>
                alert.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TreasuryAlert>> GetForExport(
        TreasuryAlertQueryDto query,
        int maxRows)
    {
        var alerts =
            _context.TreasuryAlerts
                .AsNoTracking()
                .Include(alert =>
                    alert.Account)
                .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            alerts =
                alerts.Where(alert =>
                    alert.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.AlertType))
        {
            alerts =
                alerts.Where(alert =>
                    alert.AlertType == query.AlertType);
        }

        if (!string.IsNullOrWhiteSpace(query.Severity))
        {
            alerts =
                alerts.Where(alert =>
                    alert.Severity == query.Severity);
        }

        if (query.AccountId.HasValue)
        {
            alerts =
                alerts.Where(alert =>
                    alert.AccountId == query.AccountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            alerts =
                alerts.Where(alert =>
                    alert.Currency == query.Currency);
        }

        if (query.FromUtc.HasValue)
        {
            alerts =
                alerts.Where(alert =>
                    alert.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            alerts =
                alerts.Where(alert =>
                    alert.CreatedAtUtc <= query.ToUtc.Value);
        }

        return await alerts
            .OrderByDescending(alert =>
                alert.CreatedAtUtc)
            .Take(maxRows)
            .ToListAsync();
    }

    public async Task<TreasuryAlert?> GetOpenAlert(
        string alertType,
        string? sourceEntityType,
        Guid? sourceEntityId,
        string? sourceReference)
    {
        return await _context.TreasuryAlerts
            .AsNoTracking()
            .FirstOrDefaultAsync(alert =>
                alert.Status ==
                    TreasuryAlertStatuses.Open &&
                alert.AlertType ==
                    alertType &&
                alert.SourceEntityType ==
                    sourceEntityType &&
                alert.SourceEntityId ==
                    sourceEntityId &&
                alert.SourceReference ==
                    sourceReference);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}