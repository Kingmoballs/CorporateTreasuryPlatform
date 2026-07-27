using Microsoft.Extensions.Options;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Admin;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class UserInvitationService
    : IUserInvitationService
{
    private readonly IUserInvitationRepository
        _invitationRepository;

    private readonly IUserRepository _userRepository;

    private readonly IRoleRepository _roleRepository;

    private readonly IOrganizationRepository
        _organizationRepository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IEmailSender _emailSender;

    private readonly IAuditLogService
        _auditLogService;

    private readonly UserInvitationOptions _options;

    private readonly TimeProvider _timeProvider;

    public UserInvitationService(
        IUserInvitationRepository
            invitationRepository,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IOrganizationRepository
            organizationRepository,
        ICurrentUserService currentUserService,
        IEmailSender emailSender,
        IAuditLogService auditLogService,
        IOptions<UserInvitationOptions> options,
        TimeProvider timeProvider)
    {
        _invitationRepository =
            invitationRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _organizationRepository =
            organizationRepository;
        _currentUserService =
            currentUserService;
        _emailSender = emailSender;
        _auditLogService = auditLogService;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<UserInvitationResponseDto>
        Invite(CreateUserInvitationDto dto)
    {
        var organizationId =
            GetRequiredOrganizationId();

        var email = NormalizeEmail(dto.Email);

        var role =
            await _roleRepository.GetById(
                dto.RoleId);

        if (role is null)
        {
            throw new ResourceNotFoundException(
                "Role not found.");
        }

        if (string.Equals(
                role.Name,
                Roles.PlatformAdmin,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenOperationException(
                "The PlatformAdmin role cannot be " +
                "assigned through an organization " +
                "invitation.");
        }

        var organization =
            await _organizationRepository
                .GetById(organizationId);

        if (organization is null ||
            !organization.IsActive)
        {
            throw new ResourceNotFoundException(
                "Organization not found.");
        }

        var existingUser =
            await _userRepository.GetByEmail(email);

        if (existingUser is not null &&
            existingUser.OrganizationMemberships
                .Any(membership =>
                    membership.OrganizationId ==
                        organizationId))
        {
            throw new ConflictException(
                "The user already belongs to this " +
                "organization.");
        }

        var existingInvitation =
            await _invitationRepository
                .GetActiveForEmail(
                    organizationId,
                    email);

        if (existingInvitation is not null)
        {
            throw new ConflictException(
                "A pending invitation already exists " +
                "for this email address.");
        }

        _emailSender.EnsureConfigured();

        var now = GetUtcNow();
        var rawToken = GenerateToken();

        var invitation = new UserInvitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Organization = organization,
            Email = email,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            RoleId = role.Id,
            Role = role,
            TokenHash = HashToken(rawToken),
            ExpiresAtUtc = now.AddHours(
                _options.ExpiryHours),
            InvitedByUserId =
                _currentUserService.UserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _invitationRepository.Add(
            invitation);

        await _invitationRepository.SaveChanges();

        await SendInvitation(
            invitation,
            rawToken);

        await RecordAudit(
            invitation,
            AuditActionTypes.Created,
            "User invitation created.");

        return Map(invitation, now);
    }

    public async Task<List<
        UserInvitationResponseDto>> GetPending()
    {
        var organizationId =
            GetRequiredOrganizationId();

        var invitations =
            await _invitationRepository
                .GetPending(organizationId);

        var now = GetUtcNow();

        return invitations
            .Select(invitation =>
                Map(invitation, now))
            .ToList();
    }

    public async Task<UserInvitationResponseDto>
        Resend(Guid invitationId)
    {
        var organizationId =
            GetRequiredOrganizationId();

        var invitation =
            await GetRequiredInvitation(
                organizationId,
                invitationId);

        if (invitation.AcceptedAtUtc.HasValue)
        {
            throw new ConflictException(
                "An accepted invitation cannot be " +
                "resent.");
        }

        if (invitation.RevokedAtUtc.HasValue)
        {
            throw new ConflictException(
                "A revoked invitation cannot be " +
                "resent.");
        }

        _emailSender.EnsureConfigured();

        var now = GetUtcNow();
        var rawToken = GenerateToken();

        invitation.TokenHash =
            HashToken(rawToken);

        invitation.ExpiresAtUtc =
            now.AddHours(_options.ExpiryHours);

        invitation.UpdatedAtUtc = now;
        invitation.ConcurrencyToken =
            Guid.NewGuid();

        await _invitationRepository.SaveChanges();

        await SendInvitation(
            invitation,
            rawToken);

        await RecordAudit(
            invitation,
            AuditActionTypes.Updated,
            "User invitation resent.");

        return Map(invitation, now);
    }

    public async Task Revoke(Guid invitationId)
    {
        var organizationId =
            GetRequiredOrganizationId();

        var invitation =
            await GetRequiredInvitation(
                organizationId,
                invitationId);

        if (invitation.AcceptedAtUtc.HasValue)
        {
            throw new ConflictException(
                "An accepted invitation cannot be " +
                "revoked.");
        }

        if (invitation.RevokedAtUtc.HasValue)
        {
            return;
        }

        var now = GetUtcNow();

        invitation.RevokedAtUtc = now;
        invitation.UpdatedAtUtc = now;
        invitation.ConcurrencyToken =
            Guid.NewGuid();

        await _invitationRepository.SaveChanges();

        await RecordAudit(
            invitation,
            AuditActionTypes.Updated,
            "User invitation revoked.");
    }

    public async Task<
        AcceptUserInvitationResponseDto> Accept(
            AcceptUserInvitationDto dto)
    {
        var tokenHash =
            UserInvitationTokenHelper.Hash(
                dto.Token);

        var invitation =
            await _invitationRepository
                .GetByTokenHash(tokenHash);

        var now = GetUtcNow();

        if (invitation is null ||
            invitation.AcceptedAtUtc.HasValue ||
            invitation.RevokedAtUtc.HasValue ||
            invitation.ExpiresAtUtc <= now)
        {
            throw new UnauthorizedAccessException(
                "The invitation is invalid or has " +
                "expired.");
        }

        var user =
            await _userRepository.GetByEmail(
                invitation.Email);

        var accountCreated = user is null;

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(
                    dto.Password))
            {
                throw new RequestValidationException(
                    "A password is required to create " +
                    "your account.");
            }

            user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = invitation.FirstName,
                LastName = invitation.LastName,
                Email = invitation.Email,
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        dto.Password),
                EmailVerifiedAtUtc = now,
                IsActive = true,
                RoleId = invitation.RoleId,
                Role = invitation.Role,
                CreatedAt = now
            };

            await _userRepository.Add(user);
        }
        else
        {
            user.EmailVerifiedAtUtc ??= now;
            user.IsActive = true;
        }

        var membership =
            user.OrganizationMemberships
                .FirstOrDefault(item =>
                    item.OrganizationId ==
                        invitation.OrganizationId);

        if (membership is null)
        {
            membership =
                new OrganizationMembership
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        invitation.OrganizationId,
                    Organization =
                        invitation.Organization,
                    UserId = user.Id,
                    User = user,
                    RoleId = invitation.RoleId,
                    Role = invitation.Role,
                    IsActive = true,
                    IsDefault =
                        user.OrganizationMemberships
                            .Count == 0,
                    JoinedAtUtc = now
                };

            user.OrganizationMemberships.Add(
                membership);
        }

        invitation.AcceptedAtUtc = now;
        invitation.UpdatedAtUtc = now;
        invitation.ConcurrencyToken =
            Guid.NewGuid();

        /*
         * UserRepository and UserInvitationRepository share
         * the scoped DbContext, so this single save commits
         * the user, membership and one-time token state.
         */
        await _invitationRepository.SaveChanges();

        return new AcceptUserInvitationResponseDto
        {
            Email = user.Email,
            OrganizationId =
                invitation.OrganizationId,
            OrganizationCode =
                invitation.Organization.Code,
            Role = invitation.Role.Name,
            AccountCreated = accountCreated
        };
    }

    private async Task<UserInvitation>
        GetRequiredInvitation(
            Guid organizationId,
            Guid invitationId)
    {
        var invitation =
            await _invitationRepository.GetById(
                organizationId,
                invitationId);

        if (invitation is null)
        {
            throw new ResourceNotFoundException(
                "Invitation not found.");
        }

        return invitation;
    }

    private async Task SendInvitation(
        UserInvitation invitation,
        string rawToken)
    {
        var acceptanceUrl =
            BuildAcceptanceUrl(rawToken);

        await _emailSender.SendUserInvitation(
            invitation.Email,
            $"{invitation.FirstName} " +
            invitation.LastName,
            invitation.Organization.Name,
            acceptanceUrl,
            invitation.ExpiresAtUtc);
    }

    private async Task RecordAudit(
        UserInvitation invitation,
        string action,
        string summary)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action = action,
                EntityType =
                    AuditEntityTypes.UserInvitation,
                EntityId = invitation.Id,
                EntityReference =
                    invitation.Email,
                Summary = summary,
                AfterValues = new
                {
                    invitation.Email,
                    invitation.RoleId,
                    invitation.ExpiresAtUtc,
                    invitation.AcceptedAtUtc,
                    invitation.RevokedAtUtc
                }
            });
    }

    private string BuildAcceptanceUrl(
        string rawToken)
    {
        return UserInvitationTokenHelper
            .BuildAcceptanceUrl(
                _options.AcceptanceUrl,
                rawToken);
    }

    private Guid GetRequiredOrganizationId()
    {
        var organizationId =
            _currentUserService.OrganizationId;

        if (!organizationId.HasValue ||
            organizationId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "A valid organization context is " +
                "required.");
        }

        return organizationId.Value;
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider
            .GetUtcNow()
            .UtcDateTime;
    }

    private static string GenerateToken()
    {
        return UserInvitationTokenHelper.Generate();
    }

    private static string HashToken(
        string rawToken)
    {
        return UserInvitationTokenHelper.Hash(
            rawToken);
    }

    private static string NormalizeEmail(
        string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static UserInvitationResponseDto Map(
        UserInvitation invitation,
        DateTime now)
    {
        var status =
            invitation.AcceptedAtUtc.HasValue
                ? "Accepted"
                : invitation.RevokedAtUtc.HasValue
                    ? "Revoked"
                    : invitation.ExpiresAtUtc <= now
                        ? "Expired"
                        : "Pending";

        return new UserInvitationResponseDto
        {
            Id = invitation.Id,
            FirstName = invitation.FirstName,
            LastName = invitation.LastName,
            Email = invitation.Email,
            RoleId = invitation.RoleId,
            Role = invitation.Role.Name,
            Status = status,
            ExpiresAtUtc =
                invitation.ExpiresAtUtc,
            CreatedAtUtc =
                invitation.CreatedAtUtc
        };
    }
}
