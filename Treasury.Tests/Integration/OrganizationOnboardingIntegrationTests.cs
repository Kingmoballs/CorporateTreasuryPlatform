using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.DTOs.OrganizationOnboarding;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public class OrganizationOnboardingIntegrationTests
{
    [Fact]
    public async Task
        ApprovedApplicationProvisionsTenantAndInvitation()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var now =
            new DateTimeOffset(
                2026,
                7,
                27,
                12,
                0,
                0,
                TimeSpan.Zero);

        await using var context =
            database.CreateSystemContext();

        var seeded =
            await SeedPlatformAdmin(
                context,
                now.UtcDateTime);

        var currentUser =
            CreateCurrentUser(seeded);

        var emailSender =
            new Mock<IEmailSender>();

        emailSender
            .SetupGet(sender =>
                sender.IsConfigured)
            .Returns(false);

        var platformService =
            new OrganizationOnboardingService(
                new OrganizationApplicationRepository(
                    context),
                currentUser.Object,
                Mock.Of<IClientRequestContext>(),
                emailSender.Object,
                Options.Create(
                    new UserInvitationOptions
                    {
                        ExpiryHours = 24,
                        AcceptanceUrl =
                            "https://localhost:3000/" +
                            "accept-invitation"
                    }),
                Options.Create(
                    new OrganizationOnboardingOptions
                    {
                        ApplicationsPerHour = 5,
                        ReturnManualInvitationUrlWhenEmailDisabled =
                            true
                    }),
                new FixedTimeProvider(now),
                NullLogger<
                    OrganizationOnboardingService>
                    .Instance);

        await using var anonymousContext =
            database
                .CreateContextWithoutOrganization();

        var anonymousService =
            new OrganizationOnboardingService(
                new OrganizationApplicationRepository(
                    anonymousContext),
                Mock.Of<ICurrentUserService>(),
                Mock.Of<IClientRequestContext>(),
                emailSender.Object,
                Options.Create(
                    new UserInvitationOptions
                    {
                        ExpiryHours = 24,
                        AcceptanceUrl =
                            "https://localhost:3000/" +
                            "accept-invitation"
                    }),
                Options.Create(
                    new OrganizationOnboardingOptions
                    {
                        ApplicationsPerHour = 5,
                        ReturnManualInvitationUrlWhenEmailDisabled =
                            true
                    }),
                new FixedTimeProvider(now),
                NullLogger<
                    OrganizationOnboardingService>
                    .Instance);

        var submissionKey = Guid.NewGuid();
        var submission =
            new SubmitOrganizationApplicationDto
            {
                OrganizationName =
                    "Acme Holdings Limited",
                RegistrationNumber = "RC-12345",
                TaxIdentificationNumber =
                    "TIN-12345",
                CountryCode = "ng",
                BaseCurrency = "ngn",
                AdminFirstName = "Ada",
                AdminLastName = "Okafor",
                AdminEmail =
                    "ADA.ADMIN@EXAMPLE.COM",
                ContactPhoneNumber =
                    "+2348000000000"
            };

        var application =
            await anonymousService.Submit(
                submission,
                submissionKey);

        var replay =
            await anonymousService.Submit(
                submission,
                submissionKey);

        Assert.True(replay.IsIdempotentReplay);
        Assert.Equal(
            application.Id,
            replay.Id);
        Assert.Equal(
            OrganizationApplicationStatuses
                .Submitted,
            application.Status);

        var underReview =
            await platformService.BeginReview(
                application.Id,
                new ReviewOrganizationApplicationDto
                {
                    ConcurrencyToken =
                        application
                            .ConcurrencyToken
                });

        var approval =
            await platformService.Approve(
                application.Id,
                new ApproveOrganizationApplicationDto
                {
                    ConcurrencyToken =
                        underReview
                            .ConcurrencyToken,
                    OrganizationCode = "acme",
                    OrganizationSlug =
                        "acme-holdings",
                    LegalEntityCode = "acme-le",
                    LegalEntityName =
                        "Acme Holdings Limited",
                    BusinessUnitCode =
                        "head-office",
                    BusinessUnitName =
                        "Head Office",
                    ApprovalThresholdAmount = 0,
                    RequiredApprovalCount = 1,
                    PendingRequestExpiryHours = 24
                });

        Assert.Equal(
            OrganizationApplicationStatuses
                .Approved,
            approval.Application.Status);
        Assert.False(
            approval.InvitationEmailSent);
        Assert.NotNull(
            approval
                .ManualInvitationAcceptanceUrl);

        Assert.True(
            await context.Organizations.AnyAsync(
                organization =>
                    organization.Id ==
                        approval.OrganizationId &&
                    organization.Code == "ACME"));

        Assert.True(
            await context.LegalEntities
                .IgnoreQueryFilters()
                .AnyAsync(entity =>
                    entity.Id ==
                        approval.LegalEntityId &&
                    entity.OrganizationId ==
                        approval.OrganizationId));

        Assert.True(
            await context.BusinessUnits
                .IgnoreQueryFilters()
                .AnyAsync(unit =>
                    unit.Id ==
                        approval.BusinessUnitId &&
                    unit.OrganizationId ==
                        approval.OrganizationId));

        Assert.Equal(
            7,
            await context.ApprovalPolicies
                .IgnoreQueryFilters()
                .CountAsync(policy =>
                    policy.OrganizationId ==
                        approval.OrganizationId));

        Assert.True(
            await context.AuditLogs
                .IgnoreQueryFilters()
                .AnyAsync(log =>
                    log.OrganizationId ==
                        approval.OrganizationId &&
                    log.EntityType ==
                        AuditEntityTypes
                            .Organization));

        var token =
            ExtractToken(
                approval
                    .ManualInvitationAcceptanceUrl!);

        var organizationContext =
            new Mock<IOrganizationContext>();

        organizationContext
            .SetupGet(item => item.IsSystemScope)
            .Returns(true);

        var invitationService =
            new UserInvitationService(
                new UserInvitationRepository(
                    context),
                new UserRepository(
                    context,
                    organizationContext.Object),
                new RoleRepository(context),
                new OrganizationRepository(context),
                currentUser.Object,
                emailSender.Object,
                Mock.Of<IAuditLogService>(),
                Options.Create(
                    new UserInvitationOptions
                    {
                        ExpiryHours = 24,
                        AcceptanceUrl =
                            "https://localhost:3000/" +
                            "accept-invitation"
                    }),
                new FixedTimeProvider(
                    now.AddMinutes(1)));

        var accepted =
            await invitationService.Accept(
                new AcceptUserInvitationDto
                {
                    Token = token,
                    Password = "StrongPassword!123"
                });

        Assert.True(accepted.AccountCreated);
        Assert.Equal(
            approval.OrganizationId,
            accepted.OrganizationId);
        Assert.Equal(
            Roles.Admin,
            accepted.Role);

        var adminMembership =
            await context.OrganizationMemberships
                .Include(membership =>
                    membership.User)
                .Include(membership =>
                    membership.Role)
                .SingleAsync(membership =>
                    membership.OrganizationId ==
                        approval.OrganizationId);

        Assert.Equal(
            "ada.admin@example.com",
            adminMembership.User.Email);
        Assert.Equal(
            Roles.Admin,
            adminMembership.Role.Name);
    }

    [Fact]
    public async Task
        OrganizationAdminCannotUsePlatformReviewFlow()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        await using var context =
            database.CreateSystemContext();

        var currentUser =
            new Mock<ICurrentUserService>();

        currentUser
            .SetupGet(user => user.Role)
            .Returns(Roles.Admin);

        currentUser
            .SetupGet(user =>
                user.OrganizationCode)
            .Returns(
                OrganizationDefaults
                    .OrganizationCode);

        var service =
            new OrganizationOnboardingService(
                new OrganizationApplicationRepository(
                    context),
                currentUser.Object,
                Mock.Of<IClientRequestContext>(),
                Mock.Of<IEmailSender>(),
                Options.Create(
                    new UserInvitationOptions()),
                Options.Create(
                    new OrganizationOnboardingOptions()),
                TimeProvider.System,
                NullLogger<
                    OrganizationOnboardingService>
                    .Instance);

        await Assert.ThrowsAsync<
            ForbiddenOperationException>(
            () => service.Search(
                new
                    OrganizationApplicationQueryDto()));
    }

    private static async Task<SeededPlatformAdmin>
        SeedPlatformAdmin(
            Treasury.Infrastructure.Persistence
                .TreasuryDbContext context,
            DateTime now)
    {
        var platformAdminRole =
            await context.Roles
                .FirstOrDefaultAsync(role =>
                    role.Name ==
                        Roles.PlatformAdmin);

        if (platformAdminRole is null)
        {
            platformAdminRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = Roles.PlatformAdmin
            };

            await context.Roles.AddAsync(
                platformAdminRole);
        }

        var adminRole =
            await context.Roles
                .FirstOrDefaultAsync(role =>
                    role.Name == Roles.Admin);

        if (adminRole is null)
        {
            adminRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = Roles.Admin
            };

            await context.Roles.AddAsync(
                adminRole);
        }

        var platformOrganization =
            await context.Organizations
                .FirstOrDefaultAsync(organization =>
                    organization.Code ==
                        PlatformDefaults
                            .OrganizationCode);

        if (platformOrganization is null)
        {
            platformOrganization =
                new Organization
                {
                    Id = Guid.NewGuid(),
                    Code =
                        PlatformDefaults
                            .OrganizationCode,
                    Name =
                        PlatformDefaults
                            .OrganizationName,
                    Slug =
                        PlatformDefaults
                            .OrganizationSlug,
                    CountryCode =
                        PlatformDefaults.CountryCode,
                    BaseCurrency =
                        PlatformDefaults.BaseCurrency,
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

            await context.Organizations.AddAsync(
                platformOrganization);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Platform",
            LastName = "Administrator",
            Email = "platform@example.com",
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    "StrongPassword!123"),
            EmailVerifiedAtUtc = now,
            IsActive = true,
            RoleId = platformAdminRole.Id,
            Role = platformAdminRole,
            CreatedAt = now
        };

        var membership =
            new OrganizationMembership
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    platformOrganization.Id,
                Organization =
                    platformOrganization,
                UserId = user.Id,
                User = user,
                RoleId = platformAdminRole.Id,
                Role = platformAdminRole,
                IsActive = true,
                IsDefault = true,
                JoinedAtUtc = now
            };

        user.OrganizationMemberships.Add(
            membership);

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        return new SeededPlatformAdmin(
            user.Id,
            user.Email,
            platformOrganization.Id);
    }

    private static Mock<ICurrentUserService>
        CreateCurrentUser(
            SeededPlatformAdmin seeded)
    {
        var currentUser =
            new Mock<ICurrentUserService>();

        currentUser
            .SetupGet(user => user.UserId)
            .Returns(seeded.UserId);

        currentUser
            .SetupGet(user => user.Email)
            .Returns(seeded.Email);

        currentUser
            .SetupGet(user => user.Role)
            .Returns(Roles.PlatformAdmin);

        currentUser
            .SetupGet(user =>
                user.OrganizationId)
            .Returns(seeded.OrganizationId);

        currentUser
            .SetupGet(user =>
                user.OrganizationCode)
            .Returns(
                PlatformDefaults.OrganizationCode);

        return currentUser;
    }

    private static string ExtractToken(
        string acceptanceUrl)
    {
        const string marker = "token=";

        var index =
            acceptanceUrl.IndexOf(
                marker,
                StringComparison.Ordinal);

        Assert.True(index >= 0);

        return Uri.UnescapeDataString(
            acceptanceUrl[
                (index + marker.Length)..]);
    }

    private sealed record SeededPlatformAdmin(
        Guid UserId,
        string Email,
        Guid OrganizationId);

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(
            DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }
}
