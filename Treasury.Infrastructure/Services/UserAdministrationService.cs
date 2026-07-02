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

    public UserAdministrationService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _currentUserService = currentUserService;
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
                    user.Role))
            .ToList();
    }

    public async Task<List<RoleDto>>
        GetRoles()
    {
        var roles =
            await _roleRepository.GetAll();

        return roles
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

        if (user.RoleId == role.Id)
        {
            return MapUser(user, user.Role);
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

        if (IsAdmin(user) &&
            !IsAdmin(role) &&
            user.IsActive)
        {
            await EnsureAnotherActiveAdminExists(
                user.Id);
        }

        user.RoleId = role.Id;

        await _userRepository
            .SaveChanges();

        return MapUser(user, role);
    }

    public async Task<AdminUserDto>
        SetUserStatus(
            Guid userId,
            bool isActive)
    {
        var user =
            await GetRequiredUser(userId);

        if (user.IsActive == isActive)
        {
            return MapUser(user, user.Role);
        }

        if (user.Id ==
            _currentUserService.UserId &&
            !isActive)
        {
            throw new ForbiddenOperationException(
                "You cannot deactivate " +
                "your own account.");
        }

        if (IsAdmin(user) &&
            user.IsActive &&
            !isActive)
        {
            await EnsureAnotherActiveAdminExists(
                user.Id);
        }

        user.IsActive = isActive;

        await _userRepository
            .SaveChanges();

        return MapUser(user, user.Role);
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
                user.IsActive &&
                IsAdmin(user));

        if (!anotherAdminExists)
        {
            throw new ForbiddenOperationException(
                "The last active administrator " +
                "cannot be demoted or deactivated.");
        }
    }

    private static bool IsAdmin(User user)
    {
        return string.Equals(
            user.Role.Name,
            Roles.Admin,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdmin(Role role)
    {
        return string.Equals(
            role.Name,
            Roles.Admin,
            StringComparison.OrdinalIgnoreCase);
    }

    private static AdminUserDto MapUser(
        User user,
        Role role)
    {
        return new AdminUserDto
        {
            Id = user.Id,

            FirstName = user.FirstName,

            LastName = user.LastName,

            Email = user.Email,

            RoleId = role.Id,

            Role = role.Name,

            IsActive = user.IsActive,

            CreatedAt = user.CreatedAt
        };
    }
}