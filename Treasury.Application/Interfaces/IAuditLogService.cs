using Treasury.Application.DTOs.Audit;

namespace Treasury.Application.Interfaces;

public interface IAuditLogService
{
    Task Record(CreateAuditLogDto dto);

    Task<PagedAuditLogResponseDto> Search(AuditLogQueryDto query);
}