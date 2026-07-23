namespace Treasury.Api.BackgroundServices;

public class
    AuthenticationSecurityEventRetentionOptions
{
    public const string SectionName =
        "AuthenticationSecurityEventRetention";

    public int RetentionDays { get; set; } = 90;

    public int IntervalHours { get; set; } = 24;

    public int BatchSize { get; set; } = 1000;
}
