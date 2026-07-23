using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IMultiFactorRepository
{
    Task<bool> SetPendingEnrollment(
        Guid userId,
        Guid expectedSecurityStamp,
        string protectedSecret,
        DateTime startedAtUtc);

    Task<bool> Enable(
        Guid userId,
        Guid expectedSecurityStamp,
        DateTime enabledAtUtc,
        Guid newSecurityStamp,
        IReadOnlyCollection<MfaRecoveryCode>
            recoveryCodes);

    Task<bool> ReplaceRecoveryCodes(
        Guid userId,
        Guid expectedSecurityStamp,
        DateTime changedAtUtc,
        Guid newSecurityStamp,
        IReadOnlyCollection<MfaRecoveryCode>
            recoveryCodes);

    Task<bool> Disable(
        Guid userId,
        Guid expectedSecurityStamp,
        DateTime changedAtUtc,
        Guid newSecurityStamp);

    Task<bool> TryCreateChallenge(
        MfaLoginChallenge challenge);

    Task<MfaLoginChallenge?> GetChallengeByHash(
        string tokenHash);

    Task RecordFailedChallengeAttempt(
        Guid challengeId,
        DateTime failedAtUtc,
        int maximumAttempts);

    Task<bool> ConsumeChallenge(
        Guid challengeId,
        Guid userId,
        DateTime consumedAtUtc,
        int maximumAttempts);

    Task<bool> ConsumeChallengeWithRecoveryCode(
        Guid challengeId,
        Guid userId,
        string recoveryCodeHash,
        DateTime consumedAtUtc,
        int maximumAttempts);
}
