namespace Treasury.Application.DTOs.Audit;

public class PagedAuditLogResponseDto
{
    public List<AuditLogResponseDto> Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}