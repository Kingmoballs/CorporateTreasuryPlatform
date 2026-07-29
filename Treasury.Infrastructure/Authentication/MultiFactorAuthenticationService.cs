using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Authentication;

public class MultiFactorAuthenticationService
    : IMultiFactorAuthenticationService
{
    private const int TokenByteCount = 32;
    private const int RecoveryCodeCharacterCount = 16;

    private const string RecoveryCodeAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly IMultiFactorRepository
        _repository;

    private readonly IUserRepository _userRepository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuthenticationSessionService
        _sessionService;

    private readonly ITotpService _totpService;

    private readonly IMfaSecretProtector
        _secretProtector;

    private readonly MultiFactorAuthenticationOptions
        _options;

    private readonly TimeProvider _timeProvider;

    private readonly IAuthenticationSecurityEventService
        _securityEventService;

    public MultiFactorAuthenticationService(
        IMultiFactorRepository repository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IAuthenticationSessionService sessionService,
        ITotpService totpService,
        IMfaSecretProtector secretProtector,
        IOptions<MultiFactorAuthenticationOptions>
            options,
        TimeProvider timeProvider,
        IAuthenticationSecurityEventService
            securityEventService)
    {
        _repository = repository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _sessionService = sessionService;
        _totpService = totpService;
        _secretProtector = secretProtector;
        _options = options.Value;
        _timeProvider = timeProvider;
        _securityEventService =
            securityEventService;
    }

    public async Task<AuthResponseDto>
        CreateLoginChallenge(
            User user,
            OrganizationMembership membership)
    {
        var now = GetUtcNow();
        var rawToken = GenerateToken();

        var challenge =
            new MfaLoginChallenge
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                User = user,
                OrganizationId =
                    membership.OrganizationId,
                Organization =
                    membership.Organization,
                OrganizationMembershipId =
                    membership.Id,
                OrganizationMembership =
                    membership,
                TokenHash = Hash(rawToken),
                SecurityStamp =
                    user.SecurityStamp,
                CreatedAtUtc = now,
                ExpiresAtUtc =
                    now.AddMinutes(
                        _options.ChallengeMinutes)
            };

        var created =
            await _repository.TryCreateChallenge(
                challenge);

        if (!created)
        {
            throw InvalidAuthentication();
        }

        return new AuthResponseDto
        {
            MfaRequired = true,
            MfaChallengeToken = rawToken,
            MfaChallengeExpiresAtUtc =
                challenge.ExpiresAtUtc,
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

    public async Task<AuthResponseDto>
        VerifyChallenge(
            VerifyMfaChallengeDto dto)
    {
        var challenge =
            await GetValidChallenge(
                dto.ChallengeToken);

        var now = GetUtcNow();
        var secret =
            UnprotectSecret(
                challenge.User
                    .ProtectedTotpSecret);

        if (!_totpService.Verify(
                secret,
                dto.Code,
                now))
        {
            await RecordFailedAttempt(
                challenge.Id,
                now);

            await RecordChallengeFailure(
                challenge,
                "invalid_totp");

            throw InvalidAuthentication();
        }

        var consumed =
            await _repository.ConsumeChallenge(
                challenge.Id,
                challenge.UserId,
                now,
                _options
                    .MaximumChallengeAttempts);

        if (!consumed)
        {
            throw InvalidAuthentication();
        }

        return await CreateAuthenticatedResponse(
            challenge,
            AuthenticationMethods.Totp);
    }

    public async Task<AuthResponseDto>
        UseRecoveryCode(
            UseMfaRecoveryCodeDto dto)
    {
        var challenge =
            await GetValidChallenge(
                dto.ChallengeToken);

        var now = GetUtcNow();

        var consumed =
            await _repository
                .ConsumeChallengeWithRecoveryCode(
                    challenge.Id,
                    challenge.UserId,
                    Hash(
                        NormalizeRecoveryCode(
                            dto.RecoveryCode)),
                    now,
                    _options
                        .MaximumChallengeAttempts);

        if (!consumed)
        {
            await RecordFailedAttempt(
                challenge.Id,
                now);

            await RecordChallengeFailure(
                challenge,
                "invalid_recovery_code");

            throw InvalidAuthentication();
        }

        return await CreateAuthenticatedResponse(
            challenge,
            AuthenticationMethods.RecoveryCode);
    }

    public async Task<
        StartMfaEnrollmentResponseDto>
        StartEnrollment(
            StartMfaEnrollmentDto dto)
    {
        var user = await GetCurrentUser();

        if (user.MfaEnabledAtUtc.HasValue)
        {
            throw new BusinessRuleException(
                "Multi-factor authentication is " +
                "already enabled.");
        }

        VerifyCurrentPassword(
            user,
            dto.CurrentPassword);

        var now = GetUtcNow();
        var secret =
            _totpService.GenerateSecret();

        var saved =
            await _repository
                .SetPendingEnrollment(
                    user.Id,
                    user.SecurityStamp,
                    _secretProtector.Protect(
                        secret),
                    now);

        if (!saved)
        {
            throw new ConflictException(
                "MFA enrollment state changed. " +
                "Try again.");
        }

        return new StartMfaEnrollmentResponseDto
        {
            ManualEntryKey = secret,
            ProvisioningUri =
                _totpService
                    .BuildProvisioningUri(
                        _options.Issuer,
                        user.Email,
                        secret),
            ExpiresAtUtc =
                now.AddMinutes(
                    _options.EnrollmentMinutes)
        };
    }

    public async Task<MfaRecoveryCodesResponseDto>
        ConfirmEnrollment(
            ConfirmMfaEnrollmentDto dto)
    {
        var user = await GetCurrentUser();
        var now = GetUtcNow();

        if (user.MfaEnabledAtUtc.HasValue ||
            string.IsNullOrWhiteSpace(
                user.ProtectedTotpSecret) ||
            !user.MfaEnrollmentStartedAtUtc
                .HasValue ||
            user.MfaEnrollmentStartedAtUtc <=
                now.AddMinutes(
                    -_options.EnrollmentMinutes))
        {
            throw InvalidEnrollment();
        }

        var secret =
            UnprotectSecret(
                user.ProtectedTotpSecret);

        if (!_totpService.Verify(
                secret,
                dto.Code,
                now))
        {
            throw InvalidEnrollment();
        }

        var generatedCodes =
            GenerateRecoveryCodes(
                user.Id,
                now);

        var enabled =
            await _repository.Enable(
                user.Id,
                user.SecurityStamp,
                now,
                Guid.NewGuid(),
                generatedCodes.Entities);

        if (!enabled)
        {
            throw InvalidEnrollment();
        }

        await RecordCurrentUserEvent(
            user.Id,
            AuthenticationSecurityEventTypes
                .MfaEnabled);

        return new MfaRecoveryCodesResponseDto
        {
            RecoveryCodes =
                generatedCodes.RawCodes
        };
    }

    public async Task<MfaRecoveryCodesResponseDto>
        RegenerateRecoveryCodes(
            RegenerateMfaRecoveryCodesDto dto)
    {
        var user = await GetEnabledCurrentUser();
        var now = GetUtcNow();

        VerifyCurrentPassword(
            user,
            dto.CurrentPassword);

        VerifyTotp(
            user,
            dto.Code,
            now);

        var generatedCodes =
            GenerateRecoveryCodes(
                user.Id,
                now);

        var replaced =
            await _repository.ReplaceRecoveryCodes(
                user.Id,
                user.SecurityStamp,
                now,
                Guid.NewGuid(),
                generatedCodes.Entities);

        if (!replaced)
        {
            throw InvalidAuthentication();
        }

        await RecordCurrentUserEvent(
            user.Id,
            AuthenticationSecurityEventTypes
                .MfaRecoveryCodesRegenerated);

        return new MfaRecoveryCodesResponseDto
        {
            RecoveryCodes =
                generatedCodes.RawCodes
        };
    }

    public async Task Disable(DisableMfaDto dto)
    {
        var user = await GetEnabledCurrentUser();
        var now = GetUtcNow();

        VerifyCurrentPassword(
            user,
            dto.CurrentPassword);

        VerifyTotp(
            user,
            dto.Code,
            now);

        var disabled =
            await _repository.Disable(
                user.Id,
                user.SecurityStamp,
                now,
                Guid.NewGuid());

        if (!disabled)
        {
            throw InvalidAuthentication();
        }

        await RecordCurrentUserEvent(
            user.Id,
            AuthenticationSecurityEventTypes
                .MfaDisabled);
    }

    private async Task<AuthResponseDto>
        CreateAuthenticatedResponse(
            MfaLoginChallenge challenge,
            string authenticationMethod)
    {
        var membership =
            challenge.OrganizationMembership;

        var tokens =
            await _sessionService.Create(
                challenge.User,
                membership,
                authenticationMethod);

        await _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    challenge.OrganizationId,
                UserId = challenge.UserId,
                AuthenticationSessionId =
                    tokens.AuthenticationSessionId,
                EventType =
                    AuthenticationSecurityEventTypes
                        .LoginSucceeded,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Succeeded,
                Metadata = new
                {
                    authenticationMethod
                }
            });

        if (authenticationMethod ==
            AuthenticationMethods.RecoveryCode)
        {
            await _securityEventService.Record(
                new
                    RecordAuthenticationSecurityEventDto
                    {
                        OrganizationId =
                            challenge.OrganizationId,
                        UserId = challenge.UserId,
                        AuthenticationSessionId =
                            tokens
                                .AuthenticationSessionId,
                        EventType =
                            AuthenticationSecurityEventTypes
                                .MfaRecoveryCodeUsed,
                        Outcome =
                            AuthenticationSecurityOutcomes
                                .Succeeded
                    });
        }

        return new AuthResponseDto
        {
            MfaRequired = false,
            AccessToken = tokens.AccessToken,
            RefreshTokenForCookie =
                tokens.RefreshToken,
            AccessTokenExpiresAtUtc =
                tokens.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc =
                tokens.RefreshTokenExpiresAtUtc,
            Email = challenge.User.Email,
            Role = membership.Role.Name,
            OrganizationId =
                membership.OrganizationId,
            OrganizationMembershipId =
                membership.Id,
            OrganizationCode =
                membership.Organization.Code
        };
    }

    private Task RecordChallengeFailure(
        MfaLoginChallenge challenge,
        string reasonCode)
    {
        return _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    challenge.OrganizationId,
                UserId = challenge.UserId,
                EventType =
                    AuthenticationSecurityEventTypes
                        .MfaChallengeFailed,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Failed,
                ReasonCode = reasonCode
            });
    }

    private Task RecordCurrentUserEvent(
        Guid userId,
        string eventType)
    {
        return _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    _currentUserService.OrganizationId,
                UserId = userId,
                AuthenticationSessionId =
                    _currentUserService
                        .AuthenticationSessionId,
                EventType = eventType,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Succeeded
            });
    }

    private async Task<MfaLoginChallenge>
        GetValidChallenge(string rawToken)
    {
        var challenge =
            await _repository.GetChallengeByHash(
                Hash(rawToken));

        var now = GetUtcNow();

        if (challenge is null ||
            challenge.ConsumedAtUtc.HasValue ||
            challenge.RevokedAtUtc.HasValue ||
            challenge.LockedAtUtc.HasValue ||
            challenge.FailedAttempts >=
                _options.MaximumChallengeAttempts ||
            challenge.ExpiresAtUtc <= now ||
            challenge.SecurityStamp !=
                challenge.User.SecurityStamp ||
            !challenge.User.IsActive ||
            !challenge.User
                .EmailVerifiedAtUtc.HasValue ||
            !challenge.User.MfaEnabledAtUtc.HasValue ||
            string.IsNullOrWhiteSpace(
                challenge.User
                    .ProtectedTotpSecret) ||
            !challenge.OrganizationMembership
                .IsActive ||
            !challenge.OrganizationMembership
                .Organization.IsActive)
        {
            throw InvalidAuthentication();
        }

        return challenge;
    }

    private async Task<User> GetCurrentUser()
    {
        var user =
            await _userRepository.GetById(
                _currentUserService.UserId);

        if (user is null ||
            !user.IsActive ||
            !user.EmailVerifiedAtUtc.HasValue)
        {
            throw InvalidAuthentication();
        }

        return user;
    }

    private async Task<User>
        GetEnabledCurrentUser()
    {
        var user = await GetCurrentUser();

        if (!user.MfaEnabledAtUtc.HasValue ||
            string.IsNullOrWhiteSpace(
                user.ProtectedTotpSecret))
        {
            throw new BusinessRuleException(
                "Multi-factor authentication is not " +
                "enabled.");
        }

        return user;
    }

    private void VerifyTotp(
        User user,
        string code,
        DateTime now)
    {
        var secret =
            UnprotectSecret(
                user.ProtectedTotpSecret);

        if (!_totpService.Verify(
                secret,
                code,
                now))
        {
            throw InvalidAuthentication();
        }
    }

    private static void VerifyCurrentPassword(
        User user,
        string password)
    {
        if (!BCrypt.Net.BCrypt.Verify(
                password,
                user.PasswordHash))
        {
            throw InvalidAuthentication();
        }
    }

    private string UnprotectSecret(
        string? protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(
                protectedSecret))
        {
            throw InvalidAuthentication();
        }

        try
        {
            return _secretProtector.Unprotect(
                protectedSecret);
        }
        catch (CryptographicException)
        {
            throw InvalidAuthentication();
        }
    }

    private Task RecordFailedAttempt(
        Guid challengeId,
        DateTime now)
    {
        return _repository
            .RecordFailedChallengeAttempt(
                challengeId,
                now,
                _options
                    .MaximumChallengeAttempts);
    }

    private GeneratedRecoveryCodes
        GenerateRecoveryCodes(
            Guid userId,
            DateTime createdAtUtc)
    {
        var rawCodes =
            new List<string>(
                _options.RecoveryCodeCount);

        var entities =
            new List<MfaRecoveryCode>(
                _options.RecoveryCodeCount);

        for (var index = 0;
             index < _options.RecoveryCodeCount;
             index++)
        {
            var rawCode =
                GenerateRecoveryCode();

            rawCodes.Add(rawCode);

            entities.Add(
                new MfaRecoveryCode
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CodeHash =
                        Hash(
                            NormalizeRecoveryCode(
                                rawCode)),
                    CreatedAtUtc =
                        createdAtUtc
                });
        }

        return new GeneratedRecoveryCodes(
            rawCodes,
            entities);
    }

    private static string GenerateRecoveryCode()
    {
        var randomBytes =
            RandomNumberGenerator.GetBytes(
                RecoveryCodeCharacterCount);

        var characters =
            randomBytes
                .Select(value =>
                    RecoveryCodeAlphabet[
                        value %
                        RecoveryCodeAlphabet.Length])
                .ToArray();

        return string.Join(
            '-',
            Enumerable.Range(0, 4)
                .Select(group =>
                    new string(
                        characters,
                        group * 4,
                        4)));
    }

    private static string NormalizeRecoveryCode(
        string code)
    {
        return code
            .Trim()
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToUpperInvariant();
    }

    private static string GenerateToken()
    {
        return Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(
                    TokenByteCount))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string Hash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));
    }

    private static UnauthorizedAccessException
        InvalidAuthentication()
    {
        return new UnauthorizedAccessException(
            "Multi-factor authentication failed.");
    }

    private static UnauthorizedAccessException
        InvalidEnrollment()
    {
        return new UnauthorizedAccessException(
            "The MFA enrollment code is invalid or " +
            "has expired.");
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider
            .GetUtcNow()
            .UtcDateTime;
    }

    private sealed record GeneratedRecoveryCodes(
        IReadOnlyList<string> RawCodes,
        IReadOnlyCollection<MfaRecoveryCode>
            Entities);
}
