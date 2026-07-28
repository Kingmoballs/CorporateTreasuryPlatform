using System.Text.Json;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Treasury.Api.Configuration;
using Treasury.Api.HealthChecks;
using Treasury.Api.Middleware;

namespace Treasury.Tests.OperationalReadiness;

public class DeploymentReadinessTests
{
    [Fact]
    public async Task
        CorrelationMiddleware_UsesSafeClientIdentifier()
    {
        const string correlationId =
            "client-request-123";
        var context = CreateHttpContext();
        context.Request.Headers[
            CorrelationIdMiddleware.HeaderName] =
                correlationId;
        string? downstreamTraceId = null;
        var middleware =
            new CorrelationIdMiddleware(
                async currentContext =>
                {
                    downstreamTraceId =
                        currentContext.TraceIdentifier;
                    await currentContext.Response
                        .WriteAsync("{}");
                },
                Mock.Of<ILogger<
                    CorrelationIdMiddleware>>());

        await middleware.InvokeAsync(context);

        Assert.Equal(
            correlationId,
            downstreamTraceId);
        Assert.Equal(
            correlationId,
            context.Response.Headers[
                CorrelationIdMiddleware.HeaderName]
                .ToString());
    }

    [Fact]
    public async Task
        CorrelationMiddleware_ReplacesUnsafeIdentifier()
    {
        var context = CreateHttpContext();
        context.Request.Headers[
            CorrelationIdMiddleware.HeaderName] =
                "unsafe\r\nidentifier";
        var middleware =
            new CorrelationIdMiddleware(
                currentContext =>
                    currentContext.Response
                        .WriteAsync("{}"),
                Mock.Of<ILogger<
                    CorrelationIdMiddleware>>());

        await middleware.InvokeAsync(context);

        var generated =
            context.Response.Headers[
                    CorrelationIdMiddleware.HeaderName]
                .ToString();

        Assert.NotEqual(
            "unsafe\r\nidentifier",
            generated);
        Assert.Equal(32, generated.Length);
        Assert.All(
            generated,
            character =>
                Assert.True(
                    char.IsAsciiHexDigit(character)));
    }

    [Fact]
    public async Task
        SecurityHeaders_ProductionAddsStrictPolicy()
    {
        var context = CreateHttpContext();
        var middleware =
            new SecurityHeadersMiddleware(
                currentContext =>
                    currentContext.Response
                        .WriteAsync("{}"),
                CreateEnvironment(
                    Environments.Production));

        await middleware.InvokeAsync(context);

        Assert.Equal(
            "nosniff",
            context.Response.Headers[
                "X-Content-Type-Options"]);
        Assert.Equal(
            "DENY",
            context.Response.Headers[
                "X-Frame-Options"]);
        Assert.Contains(
            "default-src 'none'",
            context.Response.Headers[
                "Content-Security-Policy"]
                .ToString());
    }

    [Fact]
    public async Task
        SecurityHeaders_DevelopmentKeepsSwaggerCompatible()
    {
        var context = CreateHttpContext();
        var middleware =
            new SecurityHeadersMiddleware(
                currentContext =>
                    currentContext.Response
                        .WriteAsync("{}"),
                CreateEnvironment(
                    Environments.Development));

        await middleware.InvokeAsync(context);

        Assert.False(
            context.Response.Headers.ContainsKey(
                "Content-Security-Policy"));
        Assert.True(
            context.Response.Headers.ContainsKey(
                "X-Content-Type-Options"));
    }

    [Fact]
    public async Task
        CorsPolicy_AllowsOnlyConfiguredOrigin()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTreasuryCors(
            new DeploymentReadinessOptions
            {
                AllowedOrigins =
                    new[]
                    {
                        "https://app.example.com/"
                    }
            });
        await using var provider =
            services.BuildServiceProvider();
        var policyProvider =
            provider.GetRequiredService<
                ICorsPolicyProvider>();
        var corsService =
            provider.GetRequiredService<ICorsService>();
        var policy = await policyProvider.GetPolicyAsync(
            new DefaultHttpContext(),
            DeploymentReadinessOptions
                .CorsPolicyName);

        Assert.NotNull(policy);

        var allowedContext =
            new DefaultHttpContext();
        allowedContext.Request.Headers.Origin =
            "https://app.example.com";
        var deniedContext =
            new DefaultHttpContext();
        deniedContext.Request.Headers.Origin =
            "https://attacker.example.com";

        Assert.True(
            corsService.EvaluatePolicy(
                    allowedContext,
                    policy)
                .IsOriginAllowed);
        Assert.False(
            corsService.EvaluatePolicy(
                    deniedContext,
                    policy)
                .IsOriginAllowed);
    }

    [Fact]
    public void
        ProductionValidation_RejectsUnsafeConfiguration()
    {
        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["JwtSettings:SecretKey"] =
                            "short",
                        ["AllowedHosts"] = "*",
                        ["BootstrapPlatformAdminOnly"] =
                            "true",
                        ["OrganizationOnboarding:" +
                         "ReturnManualInvitationUrlWhenEmailDisabled"] =
                            "true",
                        ["UserInvitations:AcceptanceUrl"] =
                            "http://localhost/invite",
                        ["PasswordRecovery:ResetUrl"] =
                            "http://localhost/reset"
                    })
                .Build();
        var options =
            new DeploymentReadinessOptions
            {
                AllowedOrigins =
                    new[]
                    {
                        "http://localhost:3000"
                    }
            };

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    ProductionConfigurationValidator
                        .Validate(
                            configuration,
                            CreateEnvironment(
                                Environments.Production),
                            options));

        Assert.Contains(
            "DefaultConnection",
            exception.Message);
        Assert.Contains(
            "AllowedHosts",
            exception.Message);
        Assert.Contains(
            "CORS",
            exception.Message);
        Assert.Contains(
            "Email delivery",
            exception.Message);
        Assert.Contains(
            "bootstrap",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Manual invitation",
            exception.Message);
    }

    [Fact]
    public void
        ProductionValidation_AcceptsCompleteConfiguration()
    {
        var secret =
            Convert.ToHexString(
                Guid.NewGuid().ToByteArray()) +
            Convert.ToHexString(
                Guid.NewGuid().ToByteArray());
        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:" +
                         "DefaultConnection"] =
                            "Host=db;Database=treasury",
                        ["JwtSettings:SecretKey"] =
                            secret,
                        ["JwtSettings:Issuer"] =
                            "https://api.example.com",
                        ["JwtSettings:Audience"] =
                            "treasury-clients",
                        ["AllowedHosts"] =
                            "api.example.com",
                        ["EmailDelivery:Enabled"] =
                            "true",
                        ["UserInvitations:AcceptanceUrl"] =
                            "https://app.example.com/invite",
                        ["PasswordRecovery:ResetUrl"] =
                            "https://app.example.com/reset"
                    })
                .Build();
        var options =
            new DeploymentReadinessOptions
            {
                AllowedOrigins =
                    new[]
                    {
                        "https://app.example.com"
                    },
                UseForwardedHeaders = true,
                TrustedProxies =
                    new[] { "10.0.0.10" },
                DataProtectionKeysPath =
                    "/persistent/keys"
            };

        ProductionConfigurationValidator.Validate(
            configuration,
            CreateEnvironment(
                Environments.Production),
            options);
    }

    [Fact]
    public void
        DevelopmentValidation_DoesNotRequireProductionSecrets()
    {
        var configuration =
            new ConfigurationBuilder().Build();

        ProductionConfigurationValidator.Validate(
            configuration,
            CreateEnvironment(
                Environments.Development),
            new DeploymentReadinessOptions());
    }

    [Fact]
    public async Task
        HealthResponse_DoesNotExposeExceptionDetails()
    {
        var context = CreateHttpContext();
        var secretException =
            new InvalidOperationException(
                "Host=secret-database");
        var report =
            new HealthReport(
                new Dictionary<
                    string,
                    HealthReportEntry>
                {
                    ["database"] =
                        new HealthReportEntry(
                            HealthStatus.Unhealthy,
                            "The database is unreachable.",
                            TimeSpan.FromMilliseconds(12),
                            secretException,
                            new Dictionary<
                                string,
                                object>())
                },
                TimeSpan.FromMilliseconds(12));

        await HealthCheckResponseWriter.Write(
            context,
            report);

        context.Response.Body.Position = 0;
        using var document =
            await JsonDocument.ParseAsync(
                context.Response.Body);
        var json =
            document.RootElement.ToString();

        Assert.Contains("Unhealthy", json);
        Assert.Contains("database", json);
        Assert.DoesNotContain(
            "secret-database",
            json);
    }

    [Fact]
    public async Task
        DatabaseReadiness_MissingDependencyIsUnhealthy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider =
            services.BuildServiceProvider();
        var check =
            new DatabaseReadinessHealthCheck(
                provider.GetRequiredService<
                    IServiceScopeFactory>(),
                provider.GetRequiredService<
                    ILogger<
                        DatabaseReadinessHealthCheck>>());

        var result = await check.CheckHealthAsync(
            new HealthCheckContext());

        Assert.Equal(
            HealthStatus.Unhealthy,
            result.Status);
        Assert.Null(result.Exception);
    }

    private static DefaultHttpContext
        CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private static IHostEnvironment
        CreateEnvironment(string name)
    {
        var environment =
            new Mock<IHostEnvironment>();
        environment
            .SetupGet(item => item.EnvironmentName)
            .Returns(name);
        return environment.Object;
    }
}
