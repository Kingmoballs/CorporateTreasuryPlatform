using Treasury.Application.DTOs.Auth;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IMultiFactorAuthenticationService
{
    Task<AuthResponseDto> CreateLoginChallenge(
        User user,
        OrganizationMembership membership);

    Task<AuthResponseDto> VerifyChallenge(
        VerifyMfaChallengeDto dto);

    Task<AuthResponseDto> UseRecoveryCode(
        UseMfaRecoveryCodeDto dto);

    Task<StartMfaEnrollmentResponseDto>
        StartEnrollment(StartMfaEnrollmentDto dto);

    Task<MfaRecoveryCodesResponseDto>
        ConfirmEnrollment(
            ConfirmMfaEnrollmentDto dto);

    Task<MfaRecoveryCodesResponseDto>
        RegenerateRecoveryCodes(
            RegenerateMfaRecoveryCodesDto dto);

    Task Disable(DisableMfaDto dto);
}
