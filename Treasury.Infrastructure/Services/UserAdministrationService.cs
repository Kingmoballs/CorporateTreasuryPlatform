using Treasury.Application.DTOs.Admin;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;
using Treasury.Application.Common.Exceptions;

namespace Treasury.Infrastructure.Services;

public class UserAdministrationService
    : IUserAdministrationService
{
    private readonly IUserRepository
        _userRepository;

    private readonly IRoleRepository
        _roleRepository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuthenticationSessionService
        _sessionService;

    public UserAdministrationService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        ICurrentUserService currentUserService,
        IAuthenticationSessionService
            sessionService)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _currentUserService = currentUserService;
        _sessionService = sessionService;
    }

    public async Task<List<AdminUserDto>>
        GetUsers()
    {
        var users =
            await _userRepository.GetAll();

        return users
            .Select(user =>
                MapUser(
                    user,
                    GetRequiredMembership(
                        user)))
            .ToList();
    }

    public async Task<List<RoleDto>>
        GetRoles()
    {
        var roles =
            await _roleRepository.GetAll();

        return roles
            .Where(role =>
                !string.Equals(
                    role.Name,
                    Roles.PlatformAdmin,
                    StringComparison.OrdinalIgnoreCase))
            .Select(role => new RoleDto
            {
                Id = role.Id,
                Name = role.Name
            })
            .ToList();
    }

    public async Task<AdminUserDto>
        AssignRole(
            Guid userId,
            Guid roleId)
    {
        var user =
            await GetRequiredUser(userId);

        var role =
            await _roleRepository
                .GetById(roleId);

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
                "The PlatformAdmin role cannot be assigned " +
                "from an organization administration endpoint.");
        }

        var membership =
            GetRequiredMembership(user);

        if (membership.RoleId == role.Id)
        {
            return MapUser(
                user,
                membership);
        }

        /*
         * Prevent an administrator from accidentally
         * removing their own administrative access.
         */
        if (user.Id ==
            _currentUserService.UserId)
        {
            throw new ForbiddenOperationException(
                "You cannot change your own role.");
        }

        if (IsAdmin(membership.Role) &&
            !IsAdmin(role) &&
            membership.IsActive)
        {
            await EnsureAnotherActiveAdminExists(
                user.Id);
        }

        membership.RoleId = role.Id;

        membership.Role = role;

        /*
         * Retain the original User.RoleId only as a
         * compatibility projection of the default tenant
         * membership.
         */
        if (membership.IsDefault)
        {
            user.RoleId = role.Id;

            user.Role = role;
        }

        await _userRepository
            .SaveChanges();

        await _sessionService
            .RevokeSessionsForMembership(
                membership.Id,
                "Organization role changed.");

        return MapUser(
            user,
            membership);
    }

    public async Task<AdminUserDto>
        SetUserStatus(
            Guid userId,
            bool isActive)
    {
        var user =
            await GetRequiredUser(userId);

        var membership =
            GetRequiredMembership(user);

        if (membership.IsActive == isActive)
        {
            return MapUser(
                user,
                membership);
        }

        if (user.Id ==
            _currentUserService.UserId &&
            !isActive)
        {
            throw new ForbiddenOperationException(
                "You cannot deactivate " +
                "your own account.");
        }

        if (IsAdmin(membership.Role) &&
            membership.IsActive &&
            !isActive)
        {
            await EnsureAnotherActiveAdminExists(
                user.Id);
        }

        membership.IsActive = isActive;

        /*
         * The global flag remains true while the user has
         * at least one active organization membership.
         */
        user.IsActive =
            user.OrganizationMemberships.Any(item =>
                item.IsActive);

        await _userRepository
            .SaveChanges();

        if (!isActive)
        {
            await _sessionService
                .RevokeSessionsForMembership(
                    membership.Id,
                    "Organization membership " +
                    "disabled.");
        }

        return MapUser(
            user,
            membership);
    }

    private async Task<User>
        GetRequiredUser(Guid userId)
    {
        var user =
            await _userRepository
                .GetById(userId);

        if (user is null)
        {
            throw new ResourceNotFoundException(
                "User not found.");
        }

        return user;
    }

    private async Task
        EnsureAnotherActiveAdminExists(
            Guid excludedUserId)
    {
        var users =
            await _userRepository.GetAll();

        var anotherAdminExists =
            users.Any(user =>
                user.Id != excludedUserId &&
                GetRequiredMembership(user)
                    .IsActive &&
                IsAdmin(
                    GetRequiredMembership(user)
                        .Role));

        if (!anotherAdminExists)
        {
            throw new ForbiddenOperationException(
                "The last active administrator " +
                "cannot be demoted or deactivated.");
        }
    }

    private static bool IsAdmin(Role role)
    {
        return string.Equals(
            role.Name,
            Roles.Admin,
            StringComparison.OrdinalIgnoreCase);
    }

    private OrganizationMembership
        GetRequiredMembership(User user)
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

        var membership =
            user.OrganizationMemberships
                .FirstOrDefault(item =>
                    item.OrganizationId ==
                        organizationId.Value);

        if (membership is null)
        {
            throw new ResourceNotFoundException(
                "User was not found in this " +
                "organization.");
        }

        return membership;
    }

    private static AdminUserDto MapUser(
        User user,
        OrganizationMembership membership)
    {
        return new AdminUserDto
        {
            Id = user.Id,

            FirstName = user.FirstName,

            LastName = user.LastName,

            Email = user.Email,

            RoleId = membership.RoleId,

            Role = membership.Role.Name,

            IsActive = membership.IsActive,

            CreatedAt = user.CreatedAt
        };
    }
}
