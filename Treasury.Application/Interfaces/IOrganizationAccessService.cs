using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Interfaces;

public interface IOrganizationAccessService
{
    Task<IReadOnlyList<
        OrganizationAccessResponseDto>>
        GetAvailableOrganizations();

    Task<AuthResponseDto> SwitchOrganization(
        SwitchOrganizationDto dto);
}
