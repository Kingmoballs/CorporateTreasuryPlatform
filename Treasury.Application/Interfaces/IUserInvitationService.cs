using Treasury.Application.DTOs.Admin;
using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Interfaces;

public interface IUserInvitationService
{
    Task<UserInvitationResponseDto> Invite(
        CreateUserInvitationDto dto);

    Task<List<UserInvitationResponseDto>>
        GetPending();

    Task<UserInvitationResponseDto> Resend(
        Guid invitationId);

    Task Revoke(Guid invitationId);

    Task<AcceptUserInvitationResponseDto> Accept(
        AcceptUserInvitationDto dto);
}
