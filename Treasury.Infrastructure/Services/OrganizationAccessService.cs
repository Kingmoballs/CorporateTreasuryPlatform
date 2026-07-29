using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class OrganizationAccessService
    : IOrganizationAccessService
{
    private readonly IOrganizationAccessRepository
        _repository;

    private readonly IAuthenticationSessionService
        _sessionService;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuthenticationSecurityEventService
        _securityEventService;

    public OrganizationAccessService(
        IOrganizationAccessRepository repository,
        IAuthenticationSessionService sessionService,
        ICurrentUserService currentUserService,
        IAuthenticationSecurityEventService
            securityEventService)
    {
        _repository = repository;
        _sessionService = sessionService;
        _currentUserService =
            currentUserService;
        _securityEventService =
            securityEventService;
    }

    public async Task<IReadOnlyList<
        OrganizationAccessResponseDto>>
        GetAvailableOrganizations()
    {
        var memberships =
            await _repository
                .GetActiveMembershipsForUser(
                    _currentUserService.UserId);

        var currentMembershipId =
            _currentUserService
                .OrganizationMembershipId;

        return memberships
            .Select(membership =>
                Map(
                    membership,
                    currentMembershipId))
            .ToList();
    }

    public async Task<AuthResponseDto>
        SwitchOrganization(
            SwitchOrganizationDto dto)
    {
        var currentSessionId =
            RequireCurrentSessionId();

        var currentMembershipId =
            _currentUserService
                .OrganizationMembershipId;

        if (!currentMembershipId.HasValue ||
            currentMembershipId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "A valid organization membership is " +
                "required.");
        }

        if (dto.OrganizationMembershipId ==
            currentMembershipId.Value)
        {
            throw new BusinessRuleException(
                "The requested organization is already " +
                "active.");
        }

        var membership =
            await _repository
                .GetActiveMembershipForUser(
                    dto.OrganizationMembershipId,
                    _currentUserService.UserId);

        if (membership is null)
        {
            /*
             * Do not reveal whether another user's
             * membership identifier exists.
             */
            throw new ResourceNotFoundException(
                "Organization access was not found.");
        }

        var tokens =
            await _sessionService
                .SwitchOrganization(
                    membership.User,
                    membership,
                    currentSessionId);

        await RecordSwitchEvents(
            membership,
            currentSessionId,
            tokens.AuthenticationSessionId);

        return MapResponse(
            membership.User,
            membership,
            tokens);
    }

    private async Task RecordSwitchEvents(
        OrganizationMembership targetMembership,
        Guid sourceSessionId,
        Guid replacementSessionId)
    {
        await _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    _currentUserService.OrganizationId,
                UserId =
                    _currentUserService.UserId,
                AuthenticationSessionId =
                    sourceSessionId,
                EventType =
                    AuthenticationSecurityEventTypes
                        .SessionRevoked,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Succeeded,
                ReasonCode =
                    "organization_switch"
            });

        await _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    targetMembership.OrganizationId,
                UserId =
                    targetMembership.UserId,
                AuthenticationSessionId =
                    replacementSessionId,
                EventType =
                    AuthenticationSecurityEventTypes
                        .OrganizationSwitched,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Succeeded,
                Metadata = new
                {
                    sourceOrganizationId =
                        _currentUserService
                            .OrganizationId,
                    sourceMembershipId =
                        _currentUserService
                            .OrganizationMembershipId
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

    private static OrganizationAccessResponseDto
        Map(
            OrganizationMembership membership,
            Guid? currentMembershipId)
    {
        return new OrganizationAccessResponseDto
        {
            OrganizationMembershipId =
                membership.Id,
            OrganizationId =
                membership.OrganizationId,
            OrganizationCode =
                membership.Organization.Code,
            OrganizationName =
                membership.Organization.Name,
            Role = membership.Role.Name,
            IsDefault = membership.IsDefault,
            IsCurrent =
                membership.Id ==
                    currentMembershipId
        };
    }

    private static AuthResponseDto MapResponse(
        User user,
        OrganizationMembership membership,
        AuthenticationTokenPairDto tokens)
    {
        return new AuthResponseDto
        {
            AccessToken = tokens.AccessToken,
            RefreshTokenForCookie =
                tokens.RefreshToken,
            AccessTokenExpiresAtUtc =
                tokens.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc =
                tokens.RefreshTokenExpiresAtUtc,
            Email = user.Email,
            Role = membership.Role.Name,
            OrganizationId =
                membership.OrganizationId,
            OrganizationMembershipId =
                membership.Id,
            OrganizationCode =
                membership.Organization.Code
        };
    }
}
