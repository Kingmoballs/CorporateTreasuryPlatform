using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly TreasuryDbContext _context;

    public AuditLogRepository(TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(AuditLog auditLog)
    {
        await _context.AuditLogs.AddAsync(auditLog);
    }

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> Search(
        AuditLogQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 50 : Math.Min(query.PageSize, 100);

        var auditLogs = _context.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (query.ActorUserId.HasValue)
        {
            auditLogs = auditLogs.Where(x => x.ActorUserId == query.ActorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            auditLogs = auditLogs.Where(x => x.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            auditLogs = auditLogs.Where(x => x.EntityType == query.EntityType);
        }

        if (query.EntityId.HasValue)
        {
            auditLogs = auditLogs.Where(x => x.EntityId == query.EntityId.Value);
        }

        if (query.FromUtc.HasValue)
        {
            auditLogs = auditLogs.Where(x => x.OccurredAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            auditLogs = auditLogs.Where(x => x.OccurredAtUtc <= query.ToUtc.Value);
        }

        var totalCount = await auditLogs.CountAsync();

        var items = await auditLogs
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}