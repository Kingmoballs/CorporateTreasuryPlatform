using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.TreasuryAlerts;
using Treasury.Application.DTOs.Exports;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class TreasuryAlertService : ITreasuryAlertService
{
    private static readonly HashSet<string> AllowedAlertTypes =
    [
        TreasuryAlertTypes.LowLiquidity,
        TreasuryAlertTypes.ForecastLiquidityGap,
        TreasuryAlertTypes.PendingApproval,
        TreasuryAlertTypes.ReconciliationException,
        TreasuryAlertTypes.FxExposure,
        TreasuryAlertTypes.AuditException,
        TreasuryAlertTypes.InvestmentMaturityUpcoming,
        TreasuryAlertTypes.InvestmentMaturityOverdue,
        TreasuryAlertTypes.InvestmentConcentration,
        TreasuryAlertTypes.InvestmentLimitWarning,
        TreasuryAlertTypes.InvestmentLimitBreach,
        TreasuryAlertTypes.CreditFacilityDebtOverdue,
        TreasuryAlertTypes.System
    ];

    private static readonly HashSet<string> AllowedSeverities =
    [
        TreasuryAlertSeverities.Info,
        TreasuryAlertSeverities.Warning,
        TreasuryAlertSeverities.Critical
    ];

    private static readonly HashSet<string> AllowedStatuses =
    [
        TreasuryAlertStatuses.Open,
        TreasuryAlertStatuses.Resolved,
        TreasuryAlertStatuses.Dismissed
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ITreasuryAlertRepository _alertRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public TreasuryAlertService(
        ITreasuryAlertRepository alertRepository,
        IAccountRepository accountRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _alertRepository = alertRepository;
        _accountRepository = accountRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<TreasuryAlertResponseDto> Create(
        CreateTreasuryAlertDto dto)
    {
        ValidateCreateDto(dto);

        Account? account =
            null;

        if (dto.AccountId.HasValue)
        {
            account =
                await _accountRepository.GetById(
                    dto.AccountId.Value);

            if (account is null)
            {
                throw new ResourceNotFoundException(
                    "Account not found.");
            }
        }

        var alert =
            new TreasuryAlert
            {
                Id =
                    Guid.NewGuid(),

                AlertType =
                    NormalizeAllowedValue(
                        dto.AlertType,
                        AllowedAlertTypes,
                        "Invalid alert type."),

                Severity =
                    NormalizeAllowedValue(
                        dto.Severity,
                        AllowedSeverities,
                        "Invalid alert severity."),

                Status =
                    TreasuryAlertStatuses.Open,

                Title =
                    dto.Title.Trim(),

                Message =
                    dto.Message.Trim(),

                AccountId =
                    dto.AccountId,

                Account =
                    account,

                Currency =
                    string.IsNullOrWhiteSpace(dto.Currency)
                        ? account?.Currency
                        : NormalizeCurrency(dto.Currency),

                SourceModule =
                    NormalizeOptionalText(dto.SourceModule),

                SourceEntityType =
                    NormalizeOptionalText(dto.SourceEntityType),

                SourceEntityId =
                    dto.SourceEntityId,

                SourceReference =
                    NormalizeOptionalText(dto.SourceReference),

                MetadataJson =
                    SerializeObject(dto.Metadata),

                CreatedByUserId =
                    TryGetCurrentUserId(),

                CreatedAtUtc =
                    DateTime.UtcNow,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

        await _alertRepository.Add(alert);
        await _alertRepository.SaveChanges();

        await RecordAlertCreatedAudit(alert);

        return Map(alert);
    }

    public async Task<PagedTreasuryAlertResponseDto> Search(
        TreasuryAlertQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter =
            ValidateOrganizationScope(
                query.AccountId,
                query.LegalEntityId,
                query.BusinessUnitId);

        if (query.FromUtc.HasValue &&
            query.ToUtc.HasValue &&
            query.FromUtc.Value > query.ToUtc.Value)
        {
            throw new BusinessRuleException(
                "Start date cannot be later than end date.");
        }

        query.Page =
            query.Page < 1 ? 1 : query.Page;

        query.PageSize =
            query.PageSize < 1 ? 50 : Math.Min(query.PageSize, 100);

        query.Status =
            string.IsNullOrWhiteSpace(query.Status)
                ? null
                : NormalizeAllowedValue(
                    query.Status,
                    AllowedStatuses,
                    "Invalid alert status.");

        query.AlertType =
            string.IsNullOrWhiteSpace(query.AlertType)
                ? null
                : NormalizeAllowedValue(
                    query.AlertType,
                    AllowedAlertTypes,
                    "Invalid alert type.");

        query.Severity =
            string.IsNullOrWhiteSpace(query.Severity)
                ? null
                : NormalizeAllowedValue(
                    query.Severity,
                    AllowedSeverities,
                    "Invalid alert severity.");

        query.Currency =
            string.IsNullOrWhiteSpace(query.Currency)
                ? null
                : NormalizeCurrency(query.Currency);

        var result =
            await _alertRepository.Search(query);

        return new PagedTreasuryAlertResponseDto
        {
            LegalEntityId =
                filter.LegalEntityId,

            BusinessUnitId =
                filter.BusinessUnitId,

            Items =
                result.Items
                    .Select(Map)
                    .ToList(),

            Page =
                query.Page,

            PageSize =
                query.PageSize,

            TotalCount =
                result.TotalCount,

            TotalPages =
                (int)Math.Ceiling(
                    result.TotalCount /
                    (double)query.PageSize)
        };
    }

    public async Task<TreasuryAlertSummaryDto> GetSummary(
        TreasuryAlertSummaryQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter =
            ValidateOrganizationScope(
                query.AccountId,
                query.LegalEntityId,
                query.BusinessUnitId);

        if (query.FromUtc.HasValue &&
            query.ToUtc.HasValue &&
            query.FromUtc.Value > query.ToUtc.Value)
        {
            throw new BusinessRuleException(
                "Start date cannot be later than end date.");
        }

        query.Currency =
            string.IsNullOrWhiteSpace(query.Currency)
                ? null
                : NormalizeCurrency(query.Currency);

        var alerts =
            await _alertRepository.GetForSummary(query);

        var openAlerts =
            alerts
                .Where(alert =>
                    alert.Status == TreasuryAlertStatuses.Open)
                .ToList();

        var todayUtc =
            DateTime.UtcNow.Date;

        return new TreasuryAlertSummaryDto
        {
            GeneratedAtUtc =
                DateTime.UtcNow,

            AccountId =
                query.AccountId,

            LegalEntityId =
                filter.LegalEntityId,

            BusinessUnitId =
                filter.BusinessUnitId,

            Currency =
                query.Currency,

            FromUtc =
                query.FromUtc,

            ToUtc =
                query.ToUtc,

            TotalAlertCount =
                alerts.Count,

            OpenAlertCount =
                openAlerts.Count,

            CriticalOpenAlertCount =
                openAlerts.Count(alert =>
                    alert.Severity == TreasuryAlertSeverities.Critical),

            WarningOpenAlertCount =
                openAlerts.Count(alert =>
                    alert.Severity == TreasuryAlertSeverities.Warning),

            InfoOpenAlertCount =
                openAlerts.Count(alert =>
                    alert.Severity == TreasuryAlertSeverities.Info),

            ResolvedAlertCount =
                alerts.Count(alert =>
                    alert.Status == TreasuryAlertStatuses.Resolved),

            DismissedAlertCount =
                alerts.Count(alert =>
                    alert.Status == TreasuryAlertStatuses.Dismissed),

            CreatedTodayCount =
                alerts.Count(alert =>
                    alert.CreatedAtUtc.Date == todayUtc),

            ByStatus =
                BuildBreakdown(
                    alerts,
                    alert => alert.Status),

            BySeverity =
                BuildBreakdown(
                    alerts,
                    alert => alert.Severity),

            ByAlertType =
                BuildBreakdown(
                    alerts,
                    alert => alert.AlertType),

            BySourceModule =
                BuildBreakdown(
                    alerts,
                    alert => alert.SourceModule ?? "Unknown"),

            LatestOpenAlerts =
                openAlerts
                    .OrderByDescending(alert =>
                        alert.CreatedAtUtc)
                    .Take(5)
                    .Select(Map)
                    .ToList()
        };
    }

    public async Task<CsvExportDto> ExportCsv(
        TreasuryAlertQueryDto query,
        int maxRows = 5000)
    {
        ArgumentNullException.ThrowIfNull(query);

        ValidateOrganizationScope(
            query.AccountId,
            query.LegalEntityId,
            query.BusinessUnitId);

        if (query.FromUtc.HasValue &&
            query.ToUtc.HasValue &&
            query.FromUtc.Value > query.ToUtc.Value)
        {
            throw new BusinessRuleException(
                "Start date cannot be later than end date.");
        }

        if (maxRows < 1 || maxRows > 50000)
        {
            throw new BusinessRuleException(
                "Max rows must be between 1 and 50000.");
        }

        query.Status =
            string.IsNullOrWhiteSpace(query.Status)
                ? null
                : NormalizeAllowedValue(
                    query.Status,
                    AllowedStatuses,
                    "Invalid alert status.");

        query.AlertType =
            string.IsNullOrWhiteSpace(query.AlertType)
                ? null
                : NormalizeAllowedValue(
                    query.AlertType,
                    AllowedAlertTypes,
                    "Invalid alert type.");

        query.Severity =
            string.IsNullOrWhiteSpace(query.Severity)
                ? null
                : NormalizeAllowedValue(
                    query.Severity,
                    AllowedSeverities,
                    "Invalid alert severity.");

        query.Currency =
            string.IsNullOrWhiteSpace(query.Currency)
                ? null
                : NormalizeCurrency(query.Currency);

        var alerts =
            await _alertRepository.GetForExport(
                query,
                maxRows);

        var builder =
            new StringBuilder();

        builder.AppendLine(
            "CreatedAtUtc,AlertType,Severity,Status,Title,Message,AccountId,AccountName,LegalEntityId,BusinessUnitId,Currency,SourceModule,SourceEntityType,SourceEntityId,SourceReference,CreatedByUserId,ClosedByUserId,ClosedAtUtc,ClosureNote,MetadataJson");

        foreach (var alert in alerts)
        {
            builder.AppendLine(
                string.Join(
                    ",",
                    new[]
                    {
                        CsvExportHelper.Escape(alert.CreatedAtUtc),
                        CsvExportHelper.Escape(alert.AlertType),
                        CsvExportHelper.Escape(alert.Severity),
                        CsvExportHelper.Escape(alert.Status),
                        CsvExportHelper.Escape(alert.Title),
                        CsvExportHelper.Escape(alert.Message),
                        CsvExportHelper.Escape(alert.AccountId),
                        CsvExportHelper.Escape(alert.Account?.Name),
                        CsvExportHelper.Escape(
                            alert.Account?.LegalEntityId),
                        CsvExportHelper.Escape(
                            alert.Account?.BusinessUnitId),
                        CsvExportHelper.Escape(alert.Currency),
                        CsvExportHelper.Escape(alert.SourceModule),
                        CsvExportHelper.Escape(alert.SourceEntityType),
                        CsvExportHelper.Escape(alert.SourceEntityId),
                        CsvExportHelper.Escape(alert.SourceReference),
                        CsvExportHelper.Escape(alert.CreatedByUserId),
                        CsvExportHelper.Escape(alert.ClosedByUserId),
                        CsvExportHelper.Escape(alert.ClosedAtUtc),
                        CsvExportHelper.Escape(alert.ClosureNote),
                        CsvExportHelper.Escape(alert.MetadataJson)
                    }));
        }

        return new CsvExportDto
        {
            FileName =
                $"treasury-alerts-{DateTime.UtcNow:yyyyMMddHHmmss}.csv",

            Content =
                CsvExportHelper.ToUtf8Bytes(
                    builder.ToString())
        };
    }

    public async Task<TreasuryAlertResponseDto> Resolve(
        Guid id,
        string? note)
    {
        return await CloseAlert(
            id,
            TreasuryAlertStatuses.Resolved,
            note);
    }

    public async Task<TreasuryAlertResponseDto> Dismiss(
        Guid id,
        string? note)
    {
        return await CloseAlert(
            id,
            TreasuryAlertStatuses.Dismissed,
            note);
    }

    private async Task<TreasuryAlertResponseDto> CloseAlert(
        Guid id,
        string status,
        string? note)
    {
        var alert =
            await _alertRepository.GetById(id);

        if (alert is null)
        {
            throw new ResourceNotFoundException(
                "Treasury alert not found.");
        }

        if (alert.Status != TreasuryAlertStatuses.Open)
        {
            throw new ConflictException(
                "Only open alerts can be closed.");
        }

        var beforeValues =
            SnapshotAlert(alert);

        alert.Status =
            status;

        alert.ClosedByUserId =
            TryGetCurrentUserId();

        alert.ClosedAtUtc =
            DateTime.UtcNow;

        alert.ClosureNote =
            NormalizeOptionalText(note);

        alert.ConcurrencyToken =
            Guid.NewGuid();

        _alertRepository.Update(alert);

        try
        {
            await _alertRepository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The alert was changed by another user.");
        }

        await RecordAlertClosedAudit(
            beforeValues,
            alert);

        return Map(alert);
    }

    private async Task RecordAlertCreatedAudit(
        TreasuryAlert alert)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Created,

                EntityType =
                    AuditEntityTypes.TreasuryAlert,

                EntityId =
                    alert.Id,

                EntityReference =
                    alert.SourceReference
                    ?? alert.Id.ToString(),

                Summary =
                    $"Treasury alert '{alert.Title}' was created.",

                AfterValues =
                    SnapshotAlert(alert),

                Metadata =
                    new
                    {
                        Module = "Treasury Alerts",
                        alert.AlertType,
                        alert.Severity
                    }
            });
    }

    private async Task RecordAlertClosedAudit(
        object beforeValues,
        TreasuryAlert alert)
    {
        var action =
            alert.Status == TreasuryAlertStatuses.Resolved
                ? AuditActionTypes.Resolved
                : AuditActionTypes.Dismissed;

        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    action,

                EntityType =
                    AuditEntityTypes.TreasuryAlert,

                EntityId =
                    alert.Id,

                EntityReference =
                    alert.SourceReference
                    ?? alert.Id.ToString(),

                Summary =
                    $"Treasury alert '{alert.Title}' was {alert.Status.ToLowerInvariant()}.",

                BeforeValues =
                    beforeValues,

                AfterValues =
                    SnapshotAlert(alert),

                Metadata =
                    new
                    {
                        Module = "Treasury Alerts",
                        alert.AlertType,
                        alert.Severity,
                        alert.Status
                    }
            });
    }

    private static List<TreasuryAlertSummaryBucketDto> BuildBreakdown(
        IReadOnlyList<TreasuryAlert> alerts,
        Func<TreasuryAlert, string> selector)
    {
        return alerts
            .GroupBy(selector)
            .Select(group =>
                new TreasuryAlertSummaryBucketDto
                {
                    Key =
                        group.Key,

                    Count =
                        group.Count()
                })
            .OrderByDescending(bucket =>
                bucket.Count)
            .ThenBy(bucket =>
                bucket.Key)
            .ToList();
    }

    private static TreasuryAlertResponseDto Map(
        TreasuryAlert alert)
    {
        return new TreasuryAlertResponseDto
        {
            Id =
                alert.Id,

            AlertType =
                alert.AlertType,

            Severity =
                alert.Severity,

            Status =
                alert.Status,

            Title =
                alert.Title,

            Message =
                alert.Message,

            AccountId =
                alert.AccountId,

            AccountName =
                alert.Account?.Name,

            LegalEntityId =
                alert.Account?.LegalEntityId,

            BusinessUnitId =
                alert.Account?.BusinessUnitId,

            Currency =
                alert.Currency,

            SourceModule =
                alert.SourceModule,

            SourceEntityType =
                alert.SourceEntityType,

            SourceEntityId =
                alert.SourceEntityId,

            SourceReference =
                alert.SourceReference,

            MetadataJson =
                alert.MetadataJson,

            CreatedByUserId =
                alert.CreatedByUserId,

            CreatedAtUtc =
                alert.CreatedAtUtc,

            ClosedByUserId =
                alert.ClosedByUserId,

            ClosedAtUtc =
                alert.ClosedAtUtc,

            ClosureNote =
                alert.ClosureNote
        };
    }

    private static object SnapshotAlert(
        TreasuryAlert alert)
    {
        return new
        {
            alert.Id,
            alert.AlertType,
            alert.Severity,
            alert.Status,
            alert.Title,
            alert.Message,
            alert.AccountId,
            AccountName = alert.Account?.Name,
            LegalEntityId =
                alert.Account?.LegalEntityId,
            BusinessUnitId =
                alert.Account?.BusinessUnitId,
            alert.Currency,
            alert.SourceModule,
            alert.SourceEntityType,
            alert.SourceEntityId,
            alert.SourceReference,
            alert.MetadataJson,
            alert.CreatedByUserId,
            alert.CreatedAtUtc,
            alert.ClosedByUserId,
            alert.ClosedAtUtc,
            alert.ClosureNote
        };
    }

    private static void ValidateCreateDto(
        CreateTreasuryAlertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.AlertType))
        {
            throw new BusinessRuleException(
                "Alert type is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Severity))
        {
            throw new BusinessRuleException(
                "Alert severity is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new BusinessRuleException(
                "Alert title is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Message))
        {
            throw new BusinessRuleException(
                "Alert message is required.");
        }
    }

    private static OrganizationDimensionFilter
        ValidateOrganizationScope(
            Guid? accountId,
            Guid? legalEntityId,
            Guid? businessUnitId)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException(
                "Account ID cannot be empty.",
                nameof(accountId));
        }

        return OrganizationDimensionFilter.Create(
            legalEntityId,
            businessUnitId);
    }

    private static string NormalizeAllowedValue(
        string value,
        HashSet<string> allowedValues,
        string errorMessage)
    {
        var normalized =
            value.Trim();

        var match =
            allowedValues.FirstOrDefault(allowed =>
                string.Equals(
                    allowed,
                    normalized,
                    StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new BusinessRuleException(
                errorMessage);
        }

        return match;
    }

    private static string NormalizeCurrency(
        string currency)
    {
        var normalized =
            currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3 ||
            !normalized.All(char.IsLetter))
        {
            throw new BusinessRuleException(
                "Currency must be a valid three-letter code.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? SerializeObject(
        object? value)
    {
        return value is null
            ? null
            : JsonSerializer.Serialize(
                value,
                JsonOptions);
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
}
