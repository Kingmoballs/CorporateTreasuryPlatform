using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class AuthenticationSecurityEventService
    : IAuthenticationSecurityEventService
{
    private static readonly JsonSerializerOptions
        JsonOptions = new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

    private static readonly HashSet<string>
        ProhibitedMetadataTerms =
            new(
                new[]
                {
                    "password",
                    "token",
                    "secret",
                    "credential",
                    "code"
                },
                StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string>
        AllowedOutcomes =
            new(
                new[]
                {
                    AuthenticationSecurityOutcomes
                        .Succeeded,
                    AuthenticationSecurityOutcomes
                        .Failed,
                    AuthenticationSecurityOutcomes
                        .Blocked
                },
                StringComparer.Ordinal);

    private readonly
        IAuthenticationSecurityEventRepository
        _repository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IClientRequestContext
        _clientRequestContext;

    private readonly TimeProvider _timeProvider;

    public AuthenticationSecurityEventService(
        IAuthenticationSecurityEventRepository
            repository,
        ICurrentUserService currentUserService,
        IClientRequestContext clientRequestContext,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUserService =
            currentUserService;
        _clientRequestContext =
            clientRequestContext;
        _timeProvider = timeProvider;
    }

    public async Task Record(
        RecordAuthenticationSecurityEventDto dto)
    {
        if (string.IsNullOrWhiteSpace(
                dto.EventType))
        {
            throw new ArgumentException(
                "Authentication security event type " +
                "is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Outcome))
        {
            throw new ArgumentException(
                "Authentication security event outcome " +
                "is required.");
        }

        if (!AllowedOutcomes.Contains(
                dto.Outcome.Trim()))
        {
            throw new ArgumentException(
                "Authentication security event outcome " +
                "is invalid.");
        }

        var metadataJson =
            SerializeMetadata(dto.Metadata);

        var item =
            new AuthenticationSecurityEvent
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    dto.OrganizationId,
                UserId = dto.UserId,
                AuthenticationSessionId =
                    dto.AuthenticationSessionId,
                EventType =
                    Truncate(
                        dto.EventType.Trim(),
                        80)!,
                Outcome =
                    Truncate(
                        dto.Outcome.Trim(),
                        20)!,
                ReasonCode =
                    Truncate(dto.ReasonCode, 100),
                IdentifierHash =
                    HashIdentifier(dto.Identifier),
                IpAddress =
                    _clientRequestContext.IpAddress,
                UserAgent =
                    _clientRequestContext.UserAgent,
                MetadataJson = metadataJson,
                OccurredAtUtc =
                    _timeProvider
                        .GetUtcNow()
                        .UtcDateTime
            };

        await _repository.Add(item);
        await _repository.SaveChanges();
    }

    public async Task<
        PagedAuthenticationSecurityEventResponseDto>
        Search(
            AuthenticationSecurityEventQueryDto query)
    {
        if (query.FromUtc.HasValue &&
            query.ToUtc.HasValue &&
            query.FromUtc.Value > query.ToUtc.Value)
        {
            throw new ArgumentException(
                "FromUtc cannot be later than ToUtc.");
        }

        var organizationId =
            _currentUserService.OrganizationId;

        if (!organizationId.HasValue ||
            organizationId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "A valid organization context is " +
                "required.");
        }

        query.Page =
            query.Page < 1 ? 1 : query.Page;

        query.PageSize =
            query.PageSize < 1
                ? 50
                : Math.Min(query.PageSize, 100);

        query.EventType =
            NormalizeFilter(query.EventType);

        query.Outcome =
            NormalizeFilter(query.Outcome);

        var result = await _repository.Search(
            organizationId.Value,
            query);

        return new
            PagedAuthenticationSecurityEventResponseDto
            {
                Items = result.Items
                    .Select(Map)
                    .ToList(),
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = result.TotalCount,
                TotalPages = (int)Math.Ceiling(
                    result.TotalCount /
                    (double)query.PageSize)
            };
    }

    public Task<int> DeleteOlderThan(
        DateTime cutoffUtc,
        int batchSize)
    {
        if (batchSize is < 1 or > 10000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                "Batch size must be between 1 and " +
                "10000.");
        }

        return _repository.DeleteOlderThan(
            cutoffUtc,
            batchSize);
    }

    private static
        AuthenticationSecurityEventResponseDto Map(
            AuthenticationSecurityEvent item)
    {
        return new
            AuthenticationSecurityEventResponseDto
            {
                Id = item.Id,
                UserId = item.UserId,
                AuthenticationSessionId =
                    item.AuthenticationSessionId,
                EventType = item.EventType,
                Outcome = item.Outcome,
                ReasonCode = item.ReasonCode,
                IpAddress = item.IpAddress,
                UserAgent = item.UserAgent,
                MetadataJson = item.MetadataJson,
                OccurredAtUtc =
                    item.OccurredAtUtc
            };
    }

    private static string? SerializeMetadata(
        object? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        var element =
            JsonSerializer.SerializeToElement(
                metadata,
                JsonOptions);

        EnsureSafeMetadata(element);

        var json = element.GetRawText();

        if (json.Length > 4000)
        {
            throw new ArgumentException(
                "Authentication security event " +
                "metadata is too large.");
        }

        return json;
    }

    private static void EnsureSafeMetadata(
        JsonElement element)
    {
        if (element.ValueKind ==
            JsonValueKind.Object)
        {
            foreach (var property in
                     element.EnumerateObject())
            {
                if (ProhibitedMetadataTerms.Any(term =>
                        property.Name.Contains(
                            term,
                            StringComparison
                                .OrdinalIgnoreCase)))
                {
                    throw new ArgumentException(
                        "Authentication security event " +
                        "metadata cannot contain secret " +
                        "material.");
                }

                EnsureSafeMetadata(property.Value);
            }
        }
        else if (element.ValueKind ==
                 JsonValueKind.Array)
        {
            foreach (var item in
                     element.EnumerateArray())
            {
                EnsureSafeMetadata(item);
            }
        }
    }

    private static string? HashIdentifier(
        string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        var normalized =
            identifier.Trim().ToUpperInvariant();

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    normalized)));
    }

    private static string? NormalizeFilter(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? Truncate(
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maximumLength
            ? trimmed
            : trimmed[..maximumLength];
    }
}
