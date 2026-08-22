using System.Net;
using Treasury.Infrastructure.Services;

namespace Treasury.Api.Configuration;

public static class ProductionConfigurationValidator
{
    public static void Validate(
        IConfiguration configuration,
        IHostEnvironment environment,
        DeploymentReadinessOptions options,
        RefreshTokenCookieOptions
            refreshTokenCookieOptions)
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
        ValidateRefreshTokenCookie(
            refreshTokenCookieOptions,
            errors);
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

    private static void ValidateRefreshTokenCookie(
        RefreshTokenCookieOptions options,
        ICollection<string> errors)
    {
        if (!options.Secure)
        {
            errors.Add(
                "The production refresh-token cookie " +
                "must use the Secure attribute.");
        }
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
            if (options.TrustForwardedHeadersFromAnyProxy)
            {
                errors.Add(
                    "TrustForwardedHeadersFromAnyProxy " +
                    "requires forwarded headers to be " +
                    "enabled.");
            }

            return;
        }

        if (options.TrustForwardedHeadersFromAnyProxy)
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

        var persistsToDatabase =
            options.PersistDataProtectionKeysToDatabase;
        var persistsToFileSystem =
            !string.IsNullOrWhiteSpace(
                options.DataProtectionKeysPath);

        if (persistsToDatabase && persistsToFileSystem)
        {
            errors.Add(
                "Configure either database or file-system " +
                "data-protection key persistence, not both.");
        }

        if (options
                .RequirePersistentDataProtectionKeysInProduction &&
            !persistsToDatabase &&
            !persistsToFileSystem)
        {
            errors.Add(
                "Persistent data-protection key storage " +
                "is required in production.");
        }
    }

    private static void ValidateEmailDelivery(
        IConfiguration configuration,
        DeploymentReadinessOptions options,
        ICollection<string> errors)
    {
        var isEnabled = configuration.GetValue<bool>(
            "EmailDelivery:Enabled");

        if (options
                .RequireEmailDeliveryInProduction &&
            !isEnabled)
        {
            errors.Add(
                "Email delivery must be enabled in " +
                "production.");
        }

        if (!isEnabled)
        {
            return;
        }

        var providerValue =
            configuration["EmailDelivery:Provider"] ??
            nameof(EmailDeliveryProvider.Smtp);
        var fromAddress =
            configuration["EmailDelivery:FromAddress"];

        if (!Enum.TryParse<EmailDeliveryProvider>(
                providerValue,
                true,
                out var provider) ||
            !Enum.IsDefined(provider))
        {
            errors.Add(
                "EmailDelivery:Provider must be " +
                "either Smtp or Resend.");
            return;
        }

        if (string.IsNullOrWhiteSpace(fromAddress) ||
            LooksLikePlaceholder(fromAddress))
        {
            errors.Add(
                "EmailDelivery:FromAddress must be a " +
                "non-placeholder sender address.");
        }

        if (provider == EmailDeliveryProvider.Resend)
        {
            ValidateResendConfiguration(
                configuration,
                errors);
            return;
        }

        if (string.IsNullOrWhiteSpace(
                configuration["EmailDelivery:Host"]))
        {
            errors.Add(
                "The SMTP email provider requires " +
                "EmailDelivery:Host.");
        }
    }

    private static void ValidateResendConfiguration(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var apiKey =
            configuration["EmailDelivery:ResendApiKey"];
        var apiBaseUrl =
            configuration[
                "EmailDelivery:ResendApiBaseUrl"] ??
            "https://api.resend.com";

        if (string.IsNullOrWhiteSpace(apiKey) ||
            LooksLikePlaceholder(apiKey))
        {
            errors.Add(
                "EmailDelivery:ResendApiKey must be a " +
                "non-placeholder secret.");
        }

        if (!Uri.TryCreate(
                apiBaseUrl,
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add(
                "EmailDelivery:ResendApiBaseUrl must " +
                "be an HTTPS URL.");
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
