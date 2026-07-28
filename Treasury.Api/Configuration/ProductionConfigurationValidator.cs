using System.Net;

namespace Treasury.Api.Configuration;

public static class ProductionConfigurationValidator
{
    public static void Validate(
        IConfiguration configuration,
        IHostEnvironment environment,
        DeploymentReadinessOptions options)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var errors = new List<string>();

        RequireDatabaseConnection(
            configuration,
            errors);
        RequireJwtConfiguration(
            configuration,
            errors);
        RequireRestrictedHosts(
            configuration,
            errors);
        RejectProductionBootstrapModes(
            configuration,
            errors);
        ValidateOrigins(options, errors);
        ValidateForwardedHeaders(options, errors);
        ValidateDataProtection(options, errors);
        ValidateEmailDelivery(
            configuration,
            options,
            errors);
        ValidateExternalUrl(
            configuration[
                "UserInvitations:AcceptanceUrl"],
            "User invitation acceptance URL",
            errors);
        ValidateExternalUrl(
            configuration[
                "PasswordRecovery:ResetUrl"],
            "Password-reset URL",
            errors);

        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Production configuration validation " +
            "failed: " +
            string.Join(" ", errors));
    }

    private static void RequireDatabaseConnection(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var connectionString =
            configuration.GetConnectionString(
                "DefaultConnection");

        if (string.IsNullOrWhiteSpace(
                connectionString) ||
            LooksLikePlaceholder(connectionString))
        {
            errors.Add(
                "ConnectionStrings:DefaultConnection " +
                "must be configured with a " +
                "non-placeholder value.");
        }
    }

    private static void RequireJwtConfiguration(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var secret =
            configuration["JwtSettings:SecretKey"];
        var issuer =
            configuration["JwtSettings:Issuer"];
        var audience =
            configuration["JwtSettings:Audience"];

        if (string.IsNullOrWhiteSpace(secret) ||
            secret.Length < 32 ||
            LooksLikePlaceholder(secret))
        {
            errors.Add(
                "JwtSettings:SecretKey must be a " +
                "non-placeholder secret containing at " +
                "least 32 characters.");
        }

        if (string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(audience))
        {
            errors.Add(
                "JWT issuer and audience are required.");
        }
    }

    private static void RequireRestrictedHosts(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var allowedHosts =
            configuration["AllowedHosts"];

        if (string.IsNullOrWhiteSpace(allowedHosts) ||
            allowedHosts
                .Split(
                    ';',
                    StringSplitOptions
                        .RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Any(host => host == "*"))
        {
            errors.Add(
                "AllowedHosts must list the production " +
                "API host names and cannot contain '*'.");
        }
    }

    private static void RejectProductionBootstrapModes(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        if (configuration.GetValue<bool>(
                "BootstrapPlatformAdminOnly") ||
            configuration.GetValue<bool>(
                "PlatformAdminBootstrap:Enabled"))
        {
            errors.Add(
                "PlatformAdmin bootstrap modes must be " +
                "disabled during normal production " +
                "startup.");
        }

        if (configuration.GetValue<bool>(
                "OrganizationOnboarding:" +
                "ReturnManualInvitationUrlWhenEmailDisabled"))
        {
            errors.Add(
                "Manual invitation URL return must be " +
                "disabled in production.");
        }
    }

    private static void ValidateOrigins(
        DeploymentReadinessOptions options,
        ICollection<string> errors)
    {
        var origins =
            options.GetNormalizedAllowedOrigins();

        if (origins.Count == 0)
        {
            errors.Add(
                "At least one production CORS origin " +
                "is required.");
            return;
        }

        if (origins.Any(origin =>
                !TryGetProductionOrigin(origin)))
        {
            errors.Add(
                "Production CORS origins must be HTTPS " +
                "origins without paths, queries, " +
                "fragments, credentials, wildcards, or " +
                "loopback hosts.");
        }
    }

    private static void ValidateForwardedHeaders(
        DeploymentReadinessOptions options,
        ICollection<string> errors)
    {
        if (options.ForwardLimit is < 1 or > 5)
        {
            errors.Add(
                "ForwardLimit must be between 1 and 5.");
        }

        if (!options.UseForwardedHeaders)
        {
            return;
        }

        var proxies =
            options.GetNormalizedTrustedProxies();

        if (proxies.Count == 0 ||
            proxies.Any(proxy =>
                !IPAddress.TryParse(proxy, out _)))
        {
            errors.Add(
                "Forwarded headers require at least " +
                "one valid trusted proxy IP address.");
        }
    }

    private static void ValidateDataProtection(
        DeploymentReadinessOptions options,
        ICollection<string> errors)
    {
        if (options.HstsMaxAgeDays is < 1 or > 730)
        {
            errors.Add(
                "HSTS max age must be between 1 and " +
                "730 days.");
        }

        if (options
                .RequirePersistentDataProtectionKeysInProduction &&
            string.IsNullOrWhiteSpace(
                options.DataProtectionKeysPath))
        {
            errors.Add(
                "A persistent data-protection key path " +
                "is required in production.");
        }
    }

    private static void ValidateEmailDelivery(
        IConfiguration configuration,
        DeploymentReadinessOptions options,
        ICollection<string> errors)
    {
        if (options
                .RequireEmailDeliveryInProduction &&
            !configuration.GetValue<bool>(
                "EmailDelivery:Enabled"))
        {
            errors.Add(
                "Email delivery must be enabled in " +
                "production.");
        }
    }

    private static void ValidateExternalUrl(
        string? value,
        string name,
        ICollection<string> errors)
    {
        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.IsLoopback)
        {
            errors.Add(
                $"{name} must be a non-loopback HTTPS " +
                "URL in production.");
        }
    }

    private static bool TryGetProductionOrigin(
        string value)
    {
        if (value.Contains('*') ||
            !Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps &&
            !uri.IsLoopback &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            uri.AbsolutePath == "/" &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment);
    }

    private static bool LooksLikePlaceholder(
        string value)
    {
        return value.Contains(
                   "change-me",
                   StringComparison.OrdinalIgnoreCase) ||
            value.Contains(
                "changeme",
                StringComparison.OrdinalIgnoreCase) ||
            value.Contains(
                "your-secret",
                StringComparison.OrdinalIgnoreCase) ||
            value.Contains(
                "configure_with",
                StringComparison.OrdinalIgnoreCase) ||
            value.Contains(
                "configure-with",
                StringComparison.OrdinalIgnoreCase) ||
            value.Contains('<') ||
            value.Contains('>');
    }
}
