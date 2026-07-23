using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class AuthenticationSecurityEventRepository
    : IAuthenticationSecurityEventRepository
{
    private readonly TreasuryDbContext _context;

    public AuthenticationSecurityEventRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        AuthenticationSecurityEvent item)
    {
        await _context.AuthenticationSecurityEvents
            .AddAsync(item);
    }

    public async Task<(
        IReadOnlyList<AuthenticationSecurityEvent>
            Items,
        int TotalCount)> Search(
            Guid organizationId,
            AuthenticationSecurityEventQueryDto query)
    {
        /*
         * Authentication events cannot use the normal
         * required-tenant query filter because anonymous
         * failures have no organization. Administrative
         * reads therefore apply the tenant predicate here.
         */
        var events =
            _context.AuthenticationSecurityEvents
                .AsNoTracking()
                .Where(item =>
                    item.OrganizationId ==
                        organizationId);

        if (query.UserId.HasValue)
        {
            events = events.Where(item =>
                item.UserId == query.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(
                query.EventType))
        {
            events = events.Where(item =>
                item.EventType == query.EventType);
        }

        if (!string.IsNullOrWhiteSpace(
                query.Outcome))
        {
            events = events.Where(item =>
                item.Outcome == query.Outcome);
        }

        if (query.FromUtc.HasValue)
        {
            events = events.Where(item =>
                item.OccurredAtUtc >=
                    query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            events = events.Where(item =>
                item.OccurredAtUtc <=
                    query.ToUtc.Value);
        }

        var totalCount = await events.CountAsync();

        var items = await events
            .OrderByDescending(item =>
                item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip((query.Page - 1) *
                query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<int> DeleteOlderThan(
        DateTime cutoffUtc,
        int batchSize)
    {
        if (!_context.IsSystemScope)
        {
            throw new UnauthorizedAccessException(
                "Authentication security-event " +
                "retention requires system scope.");
        }

        var ids = await _context
            .AuthenticationSecurityEvents
            .AsNoTracking()
            .Where(item =>
                item.OccurredAtUtc < cutoffUtc)
            .OrderBy(item => item.OccurredAtUtc)
            .Take(batchSize)
            .Select(item => item.Id)
            .ToListAsync();

        if (ids.Count == 0)
        {
            return 0;
        }

        return await _context
            .AuthenticationSecurityEvents
            .Where(item => ids.Contains(item.Id))
            .ExecuteDeleteAsync();
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
