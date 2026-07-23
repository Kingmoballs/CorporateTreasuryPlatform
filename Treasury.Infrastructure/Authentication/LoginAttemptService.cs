using Microsoft.Extensions.Options;
using Treasury.Application.Interfaces;

namespace Treasury.Infrastructure.Authentication;

public class LoginAttemptService
    : ILoginAttemptService
{
    private readonly IUserRepository _userRepository;

    private readonly AuthenticationSecurityOptions
        _options;

    private readonly TimeProvider _timeProvider;

    public LoginAttemptService(
        IUserRepository userRepository,
        IOptions<AuthenticationSecurityOptions>
            options,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public Task RecordFailure(Guid userId)
    {
        var now = GetUtcNow();

        return _userRepository.RecordFailedLogin(
            userId,
            now,
            now.AddMinutes(
                -_options
                    .LoginFailureWindowMinutes),
            _options.MaximumFailedLoginAttempts,
            now.AddMinutes(
                _options.LoginLockoutMinutes));
    }

    public Task<bool> CompleteSuccessfulAttempt(
        Guid userId)
    {
        return _userRepository
            .ClearFailedLoginsIfNotLocked(
                userId,
                GetUtcNow());
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider
            .GetUtcNow()
            .UtcDateTime;
    }
}
