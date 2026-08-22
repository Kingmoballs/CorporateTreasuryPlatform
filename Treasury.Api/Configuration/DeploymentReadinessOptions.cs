namespace Treasury.Api.Configuration;

public class DeploymentReadinessOptions
{
    public const string SectionName =
        "DeploymentReadiness";

    public const string CorsPolicyName =
        "TreasuryFrontend";

    public string[] AllowedOrigins { get; set; } =
        Array.Empty<string>();

    public bool UseForwardedHeaders { get; set; }

    public bool TrustForwardedHeadersFromAnyProxy
        { get; set; }

    public string[] TrustedProxies { get; set; } =
        Array.Empty<string>();

    public int ForwardLimit { get; set; } = 1;

    public int HstsMaxAgeDays { get; set; } = 365;

    public bool HstsIncludeSubDomains { get; set; }

    public bool HstsPreload { get; set; }

    public bool RequireEmailDeliveryInProduction
        { get; set; } = true;

    public bool
        RequirePersistentDataProtectionKeysInProduction
        { get; set; } = true;

    public string DataProtectionKeysPath
        { get; set; } = string.Empty;

    public bool PersistDataProtectionKeysToDatabase
        { get; set; }

    public bool MigrateDatabaseOnStartup { get; set; }

    public IReadOnlyList<string>
        GetNormalizedAllowedOrigins()
    {
        return AllowedOrigins
            .Where(origin =>
                !string.IsNullOrWhiteSpace(origin))
            .Select(origin =>
                origin.Trim().TrimEnd('/'))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string>
        GetNormalizedTrustedProxies()
    {
        return TrustedProxies
            .Where(proxy =>
                !string.IsNullOrWhiteSpace(proxy))
            .Select(proxy => proxy.Trim())
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
