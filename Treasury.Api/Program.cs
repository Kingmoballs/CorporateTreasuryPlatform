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


// Create a builder for the web application
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddFluentValidationAutoValidation();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<
    IOrganizationContext,
    OrganizationContext>();

builder.Services
    .AddValidatorsFromAssemblyContaining<
        RegisterDtoValidator>();


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
    IOrganizationRepository,
    OrganizationRepository>();

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
