using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[Route("api/audit-logs")]
[ApiController]
[Authorize(Roles = Roles.Admin + "," + Roles.CFO + "," + Roles.FinanceManager)]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] Guid? actorUserId,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _auditLogService.Search(new AuditLogQueryDto
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Page = page,
            PageSize = pageSize
        });

        return Ok(result);
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportAuditLogsCsv(
        [FromQuery] Guid? actorUserId,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int maxRows = 5000)
    {
        var result =
            await _auditLogService.ExportCsv(
                new AuditLogQueryDto
                {
                    ActorUserId =
                        actorUserId,

                    Action =
                        action,

                    EntityType =
                        entityType,

                    EntityId =
                        entityId,

                    FromUtc =
                        fromUtc,

                    ToUtc =
                        toUtc
                },
                maxRows);

        return File(
            result.Content,
            result.ContentType,
            result.FileName);
    }
}