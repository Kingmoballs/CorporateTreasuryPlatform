namespace Treasury.Application.Interfaces;

public interface ILoginAttemptService
{
    Task RecordFailure(Guid userId);

    Task<bool> CompleteSuccessfulAttempt(
        Guid userId);
}
