using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.Exports;

namespace Treasury.Application.Interfaces;

public interface IAuditLogService
{
    Task Record(CreateAuditLogDto dto);

    Task<PagedAuditLogResponseDto> Search(AuditLogQueryDto query);

    Task<CsvExportDto> ExportCsv(
        AuditLogQueryDto query,
        int maxRows = 5000);
}