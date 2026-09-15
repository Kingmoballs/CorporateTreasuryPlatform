using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Admin;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Authentication;

public class UserInvitationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(
            2026,
            7,
            23,
            14,
            30,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task
        Invite_PlatformAdminRoleIsRejected()
    {
        var setup = CreateSetup();

        setup.Role.Name = Roles.PlatformAdmin;

        await Assert.ThrowsAsync<
            ForbiddenOperationException>(
            () => setup.Service.Invite(
                new CreateUserInvitationDto
                {
                    FirstName = "Platform",
                    LastName = "Administrator",
                    Email =
                        "platform@example.com",
                    RoleId = setup.Role.Id
                }));

        setup.InvitationRepository.Verify(
            repository =>
                repository.Add(
                    It.IsAny<UserInvitation>()),
            Times.Never);
    }

    [Fact]
    public async Task Invite_StoresHashAndNeverReturnsRawToken()
    {
        var setup = CreateSetup();

        UserInvitation? storedInvitation = null;
        string? acceptanceUrl = null;

        setup.InvitationRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<UserInvitation>()))
            .Callback<UserInvitation>(
                invitation =>
                    storedInvitation = invitation)
            .Returns(Task.CompletedTask);

        setup.EmailSender
            .Setup(sender =>
                sender.SendUserInvitation(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime>()))
            .Callback<
                string,
                string,
                string,
                string,
                DateTime>(
                (_, _, _, url, _) =>
                    acceptanceUrl = url)
            .Returns(Task.CompletedTask);

        var response =
            await setup.Service.Invite(
                new CreateUserInvitationDto
                {
                    FirstName = "Ada",
                    LastName = "Okafor",
                    Email = " ADA@EXAMPLE.COM ",
                    RoleId = setup.Role.Id
                });

        Assert.NotNull(storedInvitation);
        Assert.NotNull(acceptanceUrl);

        var rawToken =
            GetTokenFromUrl(acceptanceUrl);

        Assert.Equal(
            64,
            storedInvitation.TokenHash.Length);

        Assert.Equal(
            HashToken(rawToken),
            storedInvitation.TokenHash);

        Assert.DoesNotContain(
            rawToken,
            storedInvitation.TokenHash,
            StringComparison.Ordinal);

        Assert.Equal(
            "ada@example.com",
            response.Email);

        Assert.DoesNotContain(
            response.GetType().GetProperties(),
            property =>
                property.Name.Contains(
                    "Token",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Null(response.ManualAcceptanceUrl);
    }

    [Fact]
    public async Task
        Invite_ManualDemoDelivery_ReturnsUrlOnceAndSkipsEmail()
    {
        var setup = CreateSetup(options =>
        {
            options.ManualDemoDeliveryEnabled = true;
            options.ManualDemoOrganizationCode =
                "MOBALLS";
        });

        UserInvitation? storedInvitation = null;

        setup.InvitationRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<UserInvitation>()))
            .Callback<UserInvitation>(invitation =>
                storedInvitation = invitation)
            .Returns(Task.CompletedTask);

        var response =
            await setup.Service.Invite(
                new CreateUserInvitationDto
                {
                    FirstName = "Demo",
                    LastName = "Recruiter",
                    Email = "recruiter@example.com",
                    RoleId = setup.Role.Id
                });

        Assert.NotNull(storedInvitation);
        Assert.NotNull(response.ManualAcceptanceUrl);

        var rawToken = GetTokenFromUrl(
            response.ManualAcceptanceUrl);

        Assert.Equal(
            HashToken(rawToken),
            storedInvitation.TokenHash);

        setup.EmailSender.Verify(
            sender => sender.EnsureConfigured(),
            Times.Never);

        setup.EmailSender.Verify(
            sender => sender.SendUserInvitation(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()),
            Times.Never);

        setup.InvitationRepository
            .Setup(repository =>
                repository.GetPending(
                    setup.Organization.Id))
            .ReturnsAsync(
                new List<UserInvitation>
                {
                    storedInvitation
                });

        var pendingInvitations =
            await setup.Service.GetPending();

        Assert.Null(
            Assert.Single(pendingInvitations)
                .ManualAcceptanceUrl);

        var pendingJson = JsonSerializer.Serialize(
            pendingInvitations);

        Assert.DoesNotContain(
            "ManualAcceptanceUrl",
            pendingJson,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task
        Invite_ManualDemoDelivery_RequiresConfiguredOrganization()
    {
        var setup = CreateSetup(options =>
        {
            options.ManualDemoDeliveryEnabled = true;
            options.ManualDemoOrganizationCode =
                "ANOTHER-ORGANIZATION";
        });

        setup.EmailSender
            .Setup(sender =>
                sender.SendUserInvitation(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var response =
            await setup.Service.Invite(
                new CreateUserInvitationDto
                {
                    FirstName = "Ada",
                    LastName = "Okafor",
                    Email = "ada@example.com",
                    RoleId = setup.Role.Id
                });

        Assert.Null(response.ManualAcceptanceUrl);

        setup.EmailSender.Verify(
            sender => sender.EnsureConfigured(),
            Times.Once);

        setup.EmailSender.Verify(
            sender => sender.SendUserInvitation(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task
        Invite_ManualDemoDelivery_RequiresTreasuryOfficerRole()
    {
        var setup = CreateSetup(options =>
        {
            options.ManualDemoDeliveryEnabled = true;
            options.ManualDemoOrganizationCode =
                "MOBALLS";
        });

        setup.Role.Name = Roles.Admin;

        setup.EmailSender
            .Setup(sender =>
                sender.SendUserInvitation(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var response =
            await setup.Service.Invite(
                new CreateUserInvitationDto
                {
                    FirstName = "Organization",
                    LastName = "Administrator",
                    Email = "admin@example.com",
                    RoleId = setup.Role.Id
                });

        Assert.Null(response.ManualAcceptanceUrl);

        setup.EmailSender.Verify(
            sender => sender.EnsureConfigured(),
            Times.Once);

        setup.EmailSender.Verify(
            sender => sender.SendUserInvitation(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task
        Resend_ManualDemoDelivery_RegeneratesAndReturnsUrl()
    {
        var setup = CreateSetup(options =>
        {
            options.ManualDemoDeliveryEnabled = true;
            options.ManualDemoOrganizationCode =
                "MOBALLS";
        });

        var oldToken = "previous-invitation-token";
        var invitation = CreateInvitation(
            setup,
            oldToken,
            Now.AddHours(1).UtcDateTime);

        setup.InvitationRepository
            .Setup(repository => repository.GetById(
                setup.Organization.Id,
                invitation.Id))
            .ReturnsAsync(invitation);

        var response =
            await setup.Service.Resend(invitation.Id);

        Assert.NotNull(response.ManualAcceptanceUrl);

        var newToken = GetTokenFromUrl(
            response.ManualAcceptanceUrl);

        Assert.NotEqual(oldToken, newToken);
        Assert.Equal(
            HashToken(newToken),
            invitation.TokenHash);

        setup.EmailSender.Verify(
            sender => sender.EnsureConfigured(),
            Times.Never);

        setup.EmailSender.Verify(
            sender => sender.SendUserInvitation(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task Accept_CreatesVerifiedUserAndConsumesToken()
    {
        var setup = CreateSetup();
        var rawToken = "single-use-invitation-token";

        var invitation =
            CreateInvitation(
                setup,
                rawToken,
                Now.AddHours(1).UtcDateTime);

        setup.InvitationRepository
            .Setup(repository =>
                repository.GetByTokenHash(
                    HashToken(rawToken)))
            .ReturnsAsync(invitation);

        User? addedUser = null;

        setup.UserRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<User>()))
            .Callback<User>(user =>
                addedUser = user)
            .Returns(Task.CompletedTask);

        var response =
            await setup.Service.Accept(
                new AcceptUserInvitationDto
                {
                    Token = rawToken,
                    Password =
                        "SecurePassword123!"
                });

        Assert.True(response.AccountCreated);
        Assert.NotNull(addedUser);
        Assert.Equal(
            Now.UtcDateTime,
            addedUser.EmailVerifiedAtUtc);
        Assert.True(
            BCrypt.Net.BCrypt.Verify(
                "SecurePassword123!",
                addedUser.PasswordHash));

        var membership =
            Assert.Single(
                addedUser.OrganizationMemberships);

        Assert.Equal(
            setup.Organization.Id,
            membership.OrganizationId);
        Assert.True(membership.IsDefault);
        Assert.Equal(
            Now.UtcDateTime,
            invitation.AcceptedAtUtc);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => setup.Service.Accept(
                new AcceptUserInvitationDto
                {
                    Token = rawToken,
                    Password =
                        "SecurePassword123!"
                }));
    }

    [Fact]
    public async Task Accept_ExistingMembershipIsNotDuplicated()
    {
        var setup = CreateSetup();
        var rawToken = "existing-user-invitation-token";

        var invitation =
            CreateInvitation(
                setup,
                rawToken,
                Now.AddHours(1).UtcDateTime);

        var originalPasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                "ExistingPassword123!");

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Ada",
            LastName = "Okafor",
            Email = invitation.Email,
            PasswordHash = originalPasswordHash,
            RoleId = setup.Role.Id,
            Role = setup.Role,
            IsActive = true
        };

        user.OrganizationMemberships.Add(
            new OrganizationMembership
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    setup.Organization.Id,
                Organization =
                    setup.Organization,
                UserId = user.Id,
                User = user,
                RoleId = setup.Role.Id,
                Role = setup.Role,
                IsActive = true,
                IsDefault = true
            });

        setup.InvitationRepository
            .Setup(repository =>
                repository.GetByTokenHash(
                    HashToken(rawToken)))
            .ReturnsAsync(invitation);

        setup.UserRepository
            .Setup(repository =>
                repository.GetByEmail(
                    invitation.Email))
            .ReturnsAsync(user);

        var response =
            await setup.Service.Accept(
                new AcceptUserInvitationDto
                {
                    Token = rawToken
                });

        Assert.False(response.AccountCreated);
        Assert.Single(
            user.OrganizationMemberships);
        Assert.Equal(
            originalPasswordHash,
            user.PasswordHash);
        Assert.Equal(
            Now.UtcDateTime,
            user.EmailVerifiedAtUtc);

        setup.UserRepository.Verify(
            repository =>
                repository.Add(
                    It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task Accept_ExpiredInvitationIsRejected()
    {
        var setup = CreateSetup();
        var rawToken = "expired-invitation-token";

        var invitation =
            CreateInvitation(
                setup,
                rawToken,
                Now.AddMinutes(-1).UtcDateTime);

        setup.InvitationRepository
            .Setup(repository =>
                repository.GetByTokenHash(
                    HashToken(rawToken)))
            .ReturnsAsync(invitation);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => setup.Service.Accept(
                new AcceptUserInvitationDto
                {
                    Token = rawToken,
                    Password =
                        "SecurePassword123!"
                }));

        setup.InvitationRepository.Verify(
            repository =>
                repository.SaveChanges(),
            Times.Never);
    }

    [Fact]
    public async Task Revoke_QueriesOnlyCurrentOrganization()
    {
        var setup = CreateSetup();
        var invitationId = Guid.NewGuid();

        setup.InvitationRepository
            .Setup(repository =>
                repository.GetById(
                    setup.Organization.Id,
                    invitationId))
            .ReturnsAsync((UserInvitation?)null);

        await Assert.ThrowsAsync<
            ResourceNotFoundException>(
            () => setup.Service.Revoke(
                invitationId));

        setup.InvitationRepository.Verify(
            repository =>
                repository.GetById(
                    setup.Organization.Id,
                    invitationId),
            Times.Once);

        setup.InvitationRepository.Verify(
            repository =>
                repository.SaveChanges(),
            Times.Never);
    }

    private static ServiceSetup CreateSetup(
        Action<UserInvitationOptions>?
            configureOptions = null)
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Code = "MOBALLS",
            Name = "Moballs Limited",
            Slug = "moballs"
        };

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = Roles.TreasuryOfficer
        };

        var invitationRepository =
            new Mock<IUserInvitationRepository>();

        invitationRepository
            .Setup(repository =>
                repository.GetActiveForEmail(
                    organization.Id,
                    It.IsAny<string>()))
            .ReturnsAsync((UserInvitation?)null);

        invitationRepository
            .Setup(repository =>
                repository.SaveChanges())
            .Returns(Task.CompletedTask);

        var userRepository =
            new Mock<IUserRepository>();

        userRepository
            .Setup(repository =>
                repository.GetByEmail(
                    It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        var roleRepository =
            new Mock<IRoleRepository>();

        roleRepository
            .Setup(repository =>
                repository.GetById(role.Id))
            .ReturnsAsync(role);

        var organizationRepository =
            new Mock<IOrganizationRepository>();

        organizationRepository
            .Setup(repository =>
                repository.GetById(
                    organization.Id))
            .ReturnsAsync(organization);

        var currentUserService =
            new Mock<ICurrentUserService>();

        currentUserService
            .SetupGet(service =>
                service.OrganizationId)
            .Returns(organization.Id);

        currentUserService
            .SetupGet(service =>
                service.UserId)
            .Returns(Guid.NewGuid());

        var emailSender =
            new Mock<IEmailSender>();

        var auditLogService =
            new Mock<IAuditLogService>();

        auditLogService
            .Setup(service =>
                service.Record(
                    It.IsAny<
                        Treasury.Application.DTOs.Audit
                            .CreateAuditLogDto>()))
            .Returns(Task.CompletedTask);

        var invitationOptions =
            new UserInvitationOptions
            {
                ExpiryHours = 24,
                AcceptanceUrl =
                    "https://treasury.example/" +
                    "accept-invitation"
            };

        configureOptions?.Invoke(invitationOptions);

        var service =
            new UserInvitationService(
                invitationRepository.Object,
                userRepository.Object,
                roleRepository.Object,
                organizationRepository.Object,
                currentUserService.Object,
                emailSender.Object,
                auditLogService.Object,
                Options.Create(invitationOptions),
                new FixedTimeProvider(Now));

        return new ServiceSetup(
            service,
            invitationRepository,
            userRepository,
            emailSender,
            organization,
            role);
    }

    private static UserInvitation
        CreateInvitation(
            ServiceSetup setup,
            string rawToken,
            DateTime expiresAtUtc)
    {
        return new UserInvitation
        {
            Id = Guid.NewGuid(),
            OrganizationId =
                setup.Organization.Id,
            Organization =
                setup.Organization,
            Email = "ada@example.com",
            FirstName = "Ada",
            LastName = "Okafor",
            RoleId = setup.Role.Id,
            Role = setup.Role,
            TokenHash = HashToken(rawToken),
            ExpiresAtUtc = expiresAtUtc,
            InvitedByUserId = Guid.NewGuid(),
            CreatedAtUtc =
                Now.AddMinutes(-5).UtcDateTime,
            UpdatedAtUtc =
                Now.AddMinutes(-5).UtcDateTime
        };
    }

    private static string GetTokenFromUrl(
        string acceptanceUrl)
    {
        var uri = new Uri(acceptanceUrl);

        var tokenParameter =
            uri.Query
                .TrimStart('?')
                .Split('&')
                .Single(parameter =>
                    parameter.StartsWith(
                        "token=",
                        StringComparison.Ordinal));

        return Uri.UnescapeDataString(
            tokenParameter["token=".Length..]);
    }

    private static string HashToken(
        string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(token)));
    }

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(
            DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset
            GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed record ServiceSetup(
        UserInvitationService Service,
        Mock<IUserInvitationRepository>
            InvitationRepository,
        Mock<IUserRepository> UserRepository,
        Mock<IEmailSender> EmailSender,
        Organization Organization,
        Role Role);
}
