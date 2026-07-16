namespace Treasury.Api.BackgroundServices;

public class InvestmentAccrualSnapshotWorkerOptions
{
    public const string SectionName =
        "InvestmentAccrualSnapshots";

    public bool Enabled { get; set; } = true;

    /*
     * The worker polls periodically and runs once
     * the configured daily UTC time has been reached.
     */
    public int CheckIntervalMinutes { get; set; } = 15;

    public int RunHourUtc { get; set; } = 22;

    public int RunMinuteUtc { get; set; } = 55;

    public string? Currency { get; set; }

    public bool IncludeRedeemed { get; set; }
}