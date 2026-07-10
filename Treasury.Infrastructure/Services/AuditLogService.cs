using System.Text.Json;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;

namespace Treasury.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentUserService _currentUserService;

    public AuditLogService(
        IAuditLogRepository auditLogRepository,
        ICurrentUserService currentUserService)
    {
        _auditLogRepository = auditLogRepository;
        _currentUserService = currentUserService;
    }

    public async Task Record(CreateAuditLogDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Action))
        {
            throw new ArgumentException("Audit action is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.EntityType))
        {
            throw new ArgumentException("Audit entity type is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Summary))
        {
            throw new ArgumentException("Audit summary is required.");
        }

        // Audit logs are append-only evidence records.
        // They are created by backend services, not directly by public API callers.
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = TryGetCurrentUserId(),
            ActorEmail = TryGetCurrentUserEmail(),
            ActorRole = TryGetCurrentUserRole(),
            Action = dto.Action.Trim(),
            EntityType = dto.EntityType.Trim(),
            EntityId = dto.EntityId,
            EntityReference = dto.EntityReference,
            Summary = dto.Summary.Trim(),
            BeforeValuesJson = SerializeObject(dto.BeforeValues),
            AfterValuesJson = SerializeObject(dto.AfterValues),
            MetadataJson = SerializeObject(dto.Metadata),
            OccurredAtUtc = DateTime.UtcNow
        };

        await _auditLogRepository.Add(auditLog);
        await _auditLogRepository.SaveChanges();
    }

    public async Task<PagedAuditLogResponseDto> Search(AuditLogQueryDto query)
    {
        if (query.FromUtc.HasValue &&
            query.ToUtc.HasValue &&
            query.FromUtc.Value > query.ToUtc.Value)
        {
            throw new ArgumentException("FromUtc cannot be later than ToUtc.");
        }

        query.Page = query.Page < 1 ? 1 : query.Page;
        query.PageSize = query.PageSize < 1 ? 50 : Math.Min(query.PageSize, 100);

        var result = await _auditLogRepository.Search(query);

        return new PagedAuditLogResponseDto
        {
            Items = result.Items.Select(Map).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = (int)Math.Ceiling(result.TotalCount / (double)query.PageSize)
        };
    }

    private static AuditLogResponseDto Map(AuditLog auditLog)
    {
        return new AuditLogResponseDto
        {
            Id = auditLog.Id,
            ActorUserId = auditLog.ActorUserId,
            ActorEmail = auditLog.ActorEmail,
            ActorRole = auditLog.ActorRole,
            Action = auditLog.Action,
            EntityType = auditLog.EntityType,
            EntityId = auditLog.EntityId,
            EntityReference = auditLog.EntityReference,
            Summary = auditLog.Summary,
            BeforeValuesJson = auditLog.BeforeValuesJson,
            AfterValuesJson = auditLog.AfterValuesJson,
            MetadataJson = auditLog.MetadataJson,
            IpAddress = auditLog.IpAddress,
            UserAgent = auditLog.UserAgent,
            OccurredAtUtc = auditLog.OccurredAtUtc
        };
    }

    private static string? SerializeObject(object? value)
    {
        return value is null
            ? null
            : JsonSerializer.Serialize(value, JsonOptions);
    }

    private Guid? TryGetCurrentUserId()
    {
        try
        {
            return _currentUserService.UserId == Guid.Empty
                ? null
                : _currentUserService.UserId;
        }
        catch
        {
            return null;
        }
    }

    private string? TryGetCurrentUserEmail()
    {
        try
        {
            return string.IsNullOrWhiteSpace(_currentUserService.Email)
                ? null
                : _currentUserService.Email;
        }
        catch
        {
            return null;
        }
    }

    private string? TryGetCurrentUserRole()
    {
        try
        {
            return string.IsNullOrWhiteSpace(_currentUserService.Role)
                ? null
                : _currentUserService.Role;
        }
        catch
        {
            return null;
        }
    }
}