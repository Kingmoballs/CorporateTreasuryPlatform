using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Treasury.Infrastructure.Persistence;
using Treasury.Application.Interfaces;
using Treasury.Infrastructure.Repositories;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Treasury.Infrastructure.Authentication;
using Treasury.Application.Services;
using Treasury.Api.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using Treasury.Application.Validators;
using Treasury.Infrastructure.Services;
using Treasury.Api.BackgroundServices;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Threading.RateLimiting;
using Treasury.Api.Models;
using Treasury.Api.Security;


// Create a builder for the web application
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddFluentValidationAutoValidation();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpContextAccessor();

var authenticationSecuritySettings =
    builder.Configuration
        .GetSection(
            AuthenticationSecurityOptions.SectionName)
        .Get<AuthenticationSecurityOptions>() ??
    new AuthenticationSecurityOptions();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy(
        AuthenticationRateLimitPolicies.Login,
        context =>
            AuthenticationRateLimitPolicies
                .CreateFixedWindowPartition(
                    context,
                    authenticationSecuritySettings
                        .LoginRequestsPerMinute));

    options.AddPolicy(
        AuthenticationRateLimitPolicies.Refresh,
        context =>
            AuthenticationRateLimitPolicies
                .CreateFixedWindowPartition(
                    context,
                    authenticationSecuritySettings
                        .RefreshRequestsPerMinute));

    options.AddPolicy(
        AuthenticationRateLimitPolicies
            .PasswordRecovery,
        context =>
            AuthenticationRateLimitPolicies
                .CreateFixedWindowPartition(
                    context,
                    authenticationSecuritySettings
                        .PasswordRecoveryRequestsPerMinute));

    options.OnRejected =
        async (rejectionContext, cancellationToken) =>
        {
            if (rejectionContext.Lease
                .TryGetMetadata(
                    MetadataName.RetryAfter,
                    out var retryAfter))
            {
                rejectionContext.HttpContext
                    .Response.Headers.RetryAfter =
                        Math.Ceiling(
                                retryAfter.TotalSeconds)
                            .ToString(
                                CultureInfo
                                    .InvariantCulture);
            }

            rejectionContext.HttpContext
                .Response.ContentType =
                    "application/json";

            await rejectionContext.HttpContext
                .Response.WriteAsJsonAsync(
                    new ApiErrorResponse
                    {
                        Code = "rate_limit_exceeded",
                        Message =
                            "Too many authentication " +
                            "requests. Try again later.",
                        TraceId =
                            rejectionContext.HttpContext
                                .TraceIdentifier
                    },
                    cancellationToken);
        };
});

builder.Services.AddScoped<
    IOrganizationContext,
    OrganizationContext>();

builder.Services
    .AddValidatorsFromAssemblyContaining<
        LoginDtoValidator>();


builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Treasury API",
            Version = "v1"
        });

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter JWT Token"
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference    
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },

                Array.Empty<string>()
            }
        });
});

builder.Services.AddScoped<
    IPendingRequestExpiryService,
    PendingRequestExpiryService>();

builder.Services
    .AddOptions<PendingRequestExpiryWorkerOptions>()
    .Bind(
        builder.Configuration.GetSection(
            PendingRequestExpiryWorkerOptions
                .SectionName))
    .Validate(
        options =>
            options.IntervalMinutes >= 1 &&
            options.IntervalMinutes <= 60,
        "Expiration interval must be between " +
        "1 and 60 minutes.")
    .ValidateOnStart();

builder.Services.AddHostedService<
    PendingRequestExpiryWorker>();


builder.Services.AddDbContext<TreasuryDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")));

builder.Services.AddScoped<
    IUserRepository,
    UserRepository>();

builder.Services.AddScoped<
    ILoginAttemptService,
    LoginAttemptService>();

builder.Services.AddScoped<
    IAuthenticationSessionRepository,
    AuthenticationSessionRepository>();

builder.Services.AddScoped<
    IAuthenticationSessionService,
    AuthenticationSessionService>();

builder.Services.AddScoped<
    IPasswordResetTokenRepository,
    PasswordResetTokenRepository>();

builder.Services.AddScoped<
    IPasswordRecoveryService,
    PasswordRecoveryService>();

builder.Services.AddScoped<
    IUserInvitationRepository,
    UserInvitationRepository>();

builder.Services.AddScoped<
    IUserInvitationService,
    UserInvitationService>();

builder.Services.AddScoped<
    IEmailSender,
    SmtpEmailSender>();

builder.Services.AddSingleton(
    TimeProvider.System);

builder.Services
    .AddOptions<AuthenticationSecurityOptions>()
    .Bind(
        builder.Configuration.GetSection(
            AuthenticationSecurityOptions
                .SectionName))
    .Validate(
        options =>
            options.MaximumFailedLoginAttempts
                is >= 3 and <= 20,
        "Maximum failed-login attempts must be " +
        "between 3 and 20.")
    .Validate(
        options =>
            options.LoginFailureWindowMinutes
                is >= 1 and <= 1440 &&
            options.LoginLockoutMinutes
                is >= 1 and <= 1440,
        "Login failure-window and lockout durations " +
        "must be between 1 and 1440 minutes.")
    .Validate(
        options =>
            options.LoginRequestsPerMinute
                is >= 1 and <= 1000 &&
            options.RefreshRequestsPerMinute
                is >= 1 and <= 1000 &&
            options.PasswordRecoveryRequestsPerMinute
                is >= 1 and <= 1000,
        "Authentication rate limits must be between " +
        "1 and 1000 requests per minute.")
    .ValidateOnStart();

builder.Services
    .AddOptions<UserInvitationOptions>()
    .Bind(
        builder.Configuration.GetSection(
            UserInvitationOptions.SectionName))
    .Validate(
        options =>
            options.ExpiryHours >= 1 &&
            options.ExpiryHours <= 168,
        "Invitation expiry must be between " +
        "1 and 168 hours.")
    .Validate(
        options =>
            Uri.TryCreate(
                options.AcceptanceUrl,
                UriKind.Absolute,
                out _),
        "Invitation acceptance URL must be an " +
        "absolute URL.")
    .ValidateOnStart();

builder.Services
    .AddOptions<PasswordRecoveryOptions>()
    .Bind(
        builder.Configuration.GetSection(
            PasswordRecoveryOptions.SectionName))
    .Validate(
        options =>
            options.TokenExpiryMinutes
                is >= 5 and <= 120,
        "Password-reset expiry must be between " +
        "5 and 120 minutes.")
    .Validate(
        options =>
            options.RequestCooldownMinutes
                is >= 1 and <= 60,
        "Password-reset request cooldown must be " +
        "between 1 and 60 minutes.")
    .Validate(
        options =>
            Uri.TryCreate(
                options.ResetUrl,
                UriKind.Absolute,
                out _),
        "Password-reset URL must be an absolute URL.")
    .ValidateOnStart();

builder.Services
    .AddOptions<EmailDeliveryOptions>()
    .Bind(
        builder.Configuration.GetSection(
            EmailDeliveryOptions.SectionName))
    .Validate(
        options =>
            !options.Enabled ||
            (!string.IsNullOrWhiteSpace(
                 options.Host) &&
             !string.IsNullOrWhiteSpace(
                 options.FromAddress) &&
             options.Port is >= 1 and <= 65535),
        "Enabled email delivery requires an SMTP " +
        "host, sender address and valid port.")
    .ValidateOnStart();

builder.Services.AddScoped<
    IOrganizationRepository,
    OrganizationRepository>();

builder.Services
    .AddOptions<JwtSettingsOptions>()
    .Bind(
        builder.Configuration.GetSection(
            JwtSettingsOptions.SectionName))
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(
                options.SecretKey) &&
            options.SecretKey.Length >= 32,
        "JWT secret key must contain at least " +
        "32 characters.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(
                options.Issuer) &&
            !string.IsNullOrWhiteSpace(
                options.Audience) &&
            options.ExpiryMinutes is >= 5 and <= 60,
        "JWT issuer, audience and an expiry between " +
        "5 and 60 minutes are required.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AuthenticationSessionOptions>()
    .Bind(
        builder.Configuration.GetSection(
            AuthenticationSessionOptions
                .SectionName))
    .Validate(
        options =>
            options.RefreshTokenDays
                is >= 1 and <= 30,
        "Refresh-token lifetime must be between " +
        "1 and 30 days.")
    .ValidateOnStart();

builder.Services.AddScoped<
    IJwtService,
    JwtService>();

builder.Services.AddScoped<
    IAuthService,
    AuthService>();

builder.Services.AddScoped<
    IRoleRepository,
    RoleRepository>();

builder.Services.AddScoped<
    ICurrentUserService,
    CurrentUserService>();

builder.Services.AddScoped<
    IAccountRepository,
    AccountRepository>();

builder.Services.AddScoped<
    IAccountTypeRepository,
    AccountTypeRepository>();

builder.Services.AddScoped<
    IAccountService,
    AccountService>();

builder.Services.AddScoped<
    ILedgerRepository,
    LedgerRepository>();

builder.Services.AddScoped<
    ITransferService,
    TransferService>();

builder.Services.AddScoped<
    ITransferRequestRepository,
    TransferRequestRepository>();

builder.Services.AddScoped<
    ITreasuryReportingService,
    TreasuryReportingService>();

builder.Services.AddScoped<
    ITreasuryTransactionRepository,
    TreasuryTransactionRepository>();

builder.Services.AddScoped<
    ITreasuryTransactionService,
    TreasuryTransactionService>();

builder.Services.AddScoped<
    IUserAdministrationService,
    UserAdministrationService>(); 

builder.Services.AddScoped<
    ICashMovementService,
    CashMovementService>();

builder.Services.AddScoped<
    IPaymentRequestRepository,
    PaymentRequestRepository>();

builder.Services.AddScoped<
    IReversalRequestRepository,
    ReversalRequestRepository>();

builder.Services.AddScoped<
    IReversalService,
    ReversalService>();

builder.Services.AddScoped<
    IApprovalPolicyRepository,
    ApprovalPolicyRepository>();

builder.Services.AddScoped<
    IApprovalPolicyService,
    ApprovalPolicyService>();

builder.Services.AddScoped<
    IApprovalDecisionRepository,
    ApprovalDecisionRepository>();

builder.Services.AddScoped<
    IApprovalHistoryService,
    ApprovalHistoryService>();

builder.Services.AddScoped<
    IBankStatementRepository,
    BankStatementRepository>();

builder.Services.AddScoped<
    IBankStatementService,
    BankStatementService>();

builder.Services.AddScoped<
    ICashFlowForecastRepository,
    CashFlowForecastRepository>();

builder.Services.AddScoped<
    ICashFlowForecastService,
    CashFlowForecastService>();

builder.Services.AddScoped<
    IInvestmentPlacementRepository,
    InvestmentPlacementRepository>();

builder.Services.AddScoped<
    IInvestmentPlacementService,
    InvestmentPlacementService>();

builder.Services.AddScoped<
    IInvestmentAccrualService,
    InvestmentAccrualService>();

builder.Services.AddScoped<
    IInvestmentEarlyRedemptionService,
    InvestmentEarlyRedemptionService>();

builder.Services.AddScoped<
    IInvestmentAccrualSnapshotRepository,
    InvestmentAccrualSnapshotRepository>();

builder.Services.AddScoped<
    IInvestmentAccrualSnapshotService,
    InvestmentAccrualSnapshotService>();

builder.Services.AddScoped<
    IInvestmentEarlyRedemptionRequestRepository,
    InvestmentEarlyRedemptionRequestRepository>();

builder.Services.AddScoped<
    IInvestmentEarlyRedemptionRequestService,
    InvestmentEarlyRedemptionRequestService>();

builder.Services.AddScoped<
    IInvestmentRolloverService,
    InvestmentRolloverService>();

builder.Services.AddScoped<
    IInvestmentRolloverRequestRepository,
    InvestmentRolloverRequestRepository>();

builder.Services.AddScoped<
    IInvestmentRolloverRequestService,
    InvestmentRolloverRequestService>();

builder.Services
    .AddOptions<
        InvestmentAccrualSnapshotWorkerOptions>()
    .Bind(
        builder.Configuration.GetSection(
            InvestmentAccrualSnapshotWorkerOptions
                .SectionName))
    .Validate(
        options =>
            options.CheckIntervalMinutes >= 1 &&
            options.CheckIntervalMinutes <= 1440,
        "Investment accrual snapshot check interval " +
        "must be between 1 and 1440 minutes.")
    .Validate(
        options =>
            options.RunHourUtc >= 0 &&
            options.RunHourUtc <= 23,
        "Investment accrual snapshot UTC hour " +
        "must be between 0 and 23.")
    .Validate(
        options =>
            options.RunMinuteUtc >= 0 &&
            options.RunMinuteUtc <= 59,
        "Investment accrual snapshot UTC minute " +
        "must be between 0 and 59.")
    .ValidateOnStart();

builder.Services.AddHostedService<
    InvestmentAccrualSnapshotWorker>();

builder.Services.AddScoped<
    IInvestmentLimitRepository,
    InvestmentLimitRepository>();

builder.Services.AddScoped<
    IInvestmentLimitService,
    InvestmentLimitService>();

builder.Services.AddScoped<
    ICounterpartyRepository,
    CounterpartyRepository>();

builder.Services.AddScoped<
    ICounterpartyService,
    CounterpartyService>();

builder.Services.AddScoped<
    IInvestmentLimitUtilizationService,
    InvestmentLimitUtilizationService>();

builder.Services.AddScoped<
    IInvestmentLimitEnforcementService,
    InvestmentLimitEnforcementService>();

builder.Services.AddScoped<
    ICreditFacilityRepository,
    CreditFacilityRepository>();

builder.Services.AddScoped<
    ICreditFacilityService,
    CreditFacilityService>();

builder.Services.AddScoped<
    ICreditFacilityDrawdownRepository,
    CreditFacilityDrawdownRepository>();

builder.Services.AddScoped<
    ICreditFacilityDrawdownService,
    CreditFacilityDrawdownService>();

builder.Services.AddScoped<
    ICreditFacilityRepaymentRepository,
    CreditFacilityRepaymentRepository>();

builder.Services.AddScoped<
    ICreditFacilityRepaymentService,
    CreditFacilityRepaymentService>();

builder.Services.AddScoped<
    ICreditFacilityInterestAccrualSnapshotRepository,
    CreditFacilityInterestAccrualSnapshotRepository>();

builder.Services.AddScoped<
    ICreditFacilityInterestAccrualService,
    CreditFacilityInterestAccrualService>();

builder.Services.AddScoped<
    ICreditFacilityLifecycleService,
    CreditFacilityLifecycleService>();

builder.Services.AddScoped<
    IFxRateRepository,
    FxRateRepository>();

builder.Services.AddScoped<
    IFxRateService,
    FxRateService>();

builder.Services.AddScoped<
    IAuditLogRepository, 
    AuditLogRepository>();

builder.Services.AddScoped<
    IAuditLogService, 
    AuditLogService>();

builder.Services.AddScoped<
    ITreasuryAlertRepository,
    TreasuryAlertRepository>();

builder.Services.AddScoped<
    ITreasuryAlertService,
    TreasuryAlertService>();

builder.Services.AddScoped<
    ITreasuryAlertMonitoringService,
    TreasuryAlertMonitoringService>();

builder.Services
    .AddOptions<TreasuryAlertMonitoringWorkerOptions>()
    .Bind(
        builder.Configuration.GetSection(
            TreasuryAlertMonitoringWorkerOptions
                .SectionName))
    .Validate(
        options =>
            options.IntervalMinutes >= 1 &&
            options.IntervalMinutes <= 1440,
        "Treasury alert monitoring interval must be between 1 and 1440 minutes.")
    .Validate(
        options =>
            options.ForecastDays >= 1 &&
            options.ForecastDays <= 180,
        "Forecast days must be between 1 and 180.")
    .Validate(
        options =>
            options.PendingApprovalAgeHours >= 1 &&
            options.PendingApprovalAgeHours <= 168,
        "Pending approval age must be between 1 and 168 hours.")
    .Validate(
        options =>
            options.ReconciliationLookbackDays >= 1 &&
            options.ReconciliationLookbackDays <= 365,
        "Reconciliation lookback days must be between 1 and 365.")
    .Validate(
        options =>
            options.InvestmentMaturityWarningDays >= 1 &&
            options.InvestmentMaturityWarningDays <= 365,
        "Investment maturity warning days must be between 1 and 365.")
    .Validate(
        options =>
            options.InvestmentConcentrationThresholdPercentage > 0 &&
            options.InvestmentConcentrationThresholdPercentage <= 100,
        "Investment concentration threshold must be greater than 0 and not greater than 100.")
    .ValidateOnStart();

builder.Services.AddHostedService<
    TreasuryAlertMonitoringWorker>();

var jwtKey = builder.Configuration[
    "JwtSettings:SecretKey"];

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,

                ValidateAudience = true,

                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,

                ClockSkew =
                    TimeSpan.FromSeconds(30),

                ValidIssuer =
                    builder.Configuration[
                        "JwtSettings:Issuer"],

                ValidAudience =
                    builder.Configuration[
                        "JwtSettings:Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtKey!))
            };
    });

// Build the web application
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionMiddleware>();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();

app.UseMiddleware<ActiveUserMiddleware>();

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context =
        scope.ServiceProvider
            .GetRequiredService<
                TreasuryDbContext>();

    await RoleSeeder.SeedRoles(context);

    await OrganizationSeeder.Seed(context);

    await AccountTypeSeeder.SeedAccountTypes(context);

    await ApprovalPolicySeeder.Seed(context);
}

app.Run();
