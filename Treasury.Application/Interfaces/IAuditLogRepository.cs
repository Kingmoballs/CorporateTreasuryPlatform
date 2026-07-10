using Treasury.Application.DTOs.Audit;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IAuditLogRepository
{
    Task Add(AuditLog auditLog);

    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> Search(
        AuditLogQueryDto query);

    Task SaveChanges();
}