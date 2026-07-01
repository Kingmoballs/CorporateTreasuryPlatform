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


// Create a builder for the web application
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddFluentValidationAutoValidation();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpContextAccessor();

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


builder.Services.AddDbContext<TreasuryDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")));

builder.Services.AddScoped<
    IUserRepository,
    UserRepository>();

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

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context =
        scope.ServiceProvider
            .GetRequiredService<
                TreasuryDbContext>();

    await RoleSeeder.SeedRoles(context);

    await AccountTypeSeeder.SeedAccountTypes(context);
}

app.Run();