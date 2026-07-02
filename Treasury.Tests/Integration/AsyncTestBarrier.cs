namespace Treasury.Tests.Integration;

public sealed class AsyncTestBarrier
{
    private readonly int _participantCount;

    private int _arrived;

    private readonly TaskCompletionSource
        _release =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

    public AsyncTestBarrier(
        int participantCount)
    {
        _participantCount =
            participantCount;
    }

    public Task SignalAndWaitAsync()
    {
        var arrived =
            Interlocked.Increment(
                ref _arrived);

        if (arrived ==
            _participantCount)
        {
            _release.TrySetResult();
        }

        return _release.Task;
    }
}