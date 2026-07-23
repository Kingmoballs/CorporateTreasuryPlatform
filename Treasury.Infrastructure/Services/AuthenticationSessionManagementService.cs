using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class AuthenticationSessionManagementService
    : IAuthenticationSessionManagementService
{
    private readonly IAuthenticationSessionRepository
        _repository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuthenticationSecurityEventService
        _securityEventService;

    private readonly TimeProvider _timeProvider;

    public AuthenticationSessionManagementService(
        IAuthenticationSessionRepository repository,
        ICurrentUserService currentUserService,
        IAuthenticationSecurityEventService
            securityEventService,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUserService =
            currentUserService;
        _securityEventService =
            securityEventService;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<
        AuthenticationSessionResponseDto>>
        GetActiveSessions()
    {
        var currentSessionId =
            RequireCurrentSessionId();

        var sessions = await _repository
            .GetActiveSessionsForUser(
                _currentUserService.UserId,
                GetUtcNow());

        return sessions.Select(session =>
                Map(session, currentSessionId))
            .ToList();
    }

    public async Task RevokeOwnedSession(
        Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ResourceNotFoundException(
                "Authentication session was not " +
                "found.");
        }

        var currentSessionId =
            RequireCurrentSessionId();

        var sessions = await _repository
            .GetActiveSessionsForUser(
                _currentUserService.UserId,
                GetUtcNow());

        var target = sessions.FirstOrDefault(
            session => session.Id == sessionId);

        if (target is null)
        {
            throw new ResourceNotFoundException(
                "Authentication session was not " +
                "found.");
        }

        var revoked =
            await _repository.RevokeOwnedSession(
                sessionId,
                _currentUserService.UserId,
                GetUtcNow(),
                sessionId == currentSessionId
                    ? "Current session revoked by user."
                    : "Session revoked by user.");

        if (!revoked)
        {
            throw new ResourceNotFoundException(
                "Authentication session was not " +
                "found.");
        }

        await _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    target.OrganizationId,
                UserId = target.UserId,
                AuthenticationSessionId =
                    target.Id,
                EventType =
                    AuthenticationSecurityEventTypes
                        .SessionRevoked,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Succeeded,
                ReasonCode =
                    sessionId == currentSessionId
                        ? "user_revoked_current"
                        : "user_revoked_session"
            });
    }

    public async Task RevokeOtherSessions()
    {
        var currentSessionId =
            RequireCurrentSessionId();

        var count =
            await _repository.RevokeOtherSessions(
                _currentUserService.UserId,
                currentSessionId,
                GetUtcNow(),
                "User signed out from other sessions.");

        await _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    _currentUserService.OrganizationId,
                UserId =
                    _currentUserService.UserId,
                AuthenticationSessionId =
                    currentSessionId,
                EventType =
                    AuthenticationSecurityEventTypes
                        .SessionRevoked,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Succeeded,
                ReasonCode =
                    "user_revoked_other_sessions",
                Metadata = new
                {
                    revokedSessionCount = count,
                    scope = "other_sessions"
                }
            });
    }

    private Guid RequireCurrentSessionId()
    {
        var sessionId =
            _currentUserService
                .AuthenticationSessionId;

        if (!sessionId.HasValue ||
            sessionId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "A valid authentication session is " +
                "required.");
        }

        return sessionId.Value;
    }

    private static
        AuthenticationSessionResponseDto Map(
            AuthenticationSession session,
            Guid currentSessionId)
    {
        return new AuthenticationSessionResponseDto
        {
            Id = session.Id,
            OrganizationId =
                session.OrganizationId,
            OrganizationCode =
                session.Organization.Code,
            AuthenticationMethod =
                session.AuthenticationMethod,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent,
            CreatedAtUtc = session.CreatedAtUtc,
            LastActivityAtUtc =
                session.LastActivityAtUtc,
            ExpiresAtUtc = session.ExpiresAtUtc,
            IsCurrent =
                session.Id == currentSessionId
        };
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider
            .GetUtcNow()
            .UtcDateTime;
    }
}
