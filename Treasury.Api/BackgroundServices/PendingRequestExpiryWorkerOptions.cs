namespace Treasury.Api.BackgroundServices;

public class PendingRequestExpiryWorkerOptions
{
    public const string SectionName =
        "PendingRequestExpiry";

    public int IntervalMinutes { get; set; }
        = 5;
}