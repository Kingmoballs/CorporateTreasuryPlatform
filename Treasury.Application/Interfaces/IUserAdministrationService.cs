using Treasury.Application.DTOs.Admin;

namespace Treasury.Application.Interfaces;

public interface IUserAdministrationService
{
    Task<List<AdminUserDto>> GetUsers();

    Task<List<RoleDto>> GetRoles();

    Task<AdminUserDto> AssignRole(
        Guid userId,
        Guid roleId);

    Task<AdminUserDto> SetUserStatus(
        Guid userId,
        bool isActive);
}