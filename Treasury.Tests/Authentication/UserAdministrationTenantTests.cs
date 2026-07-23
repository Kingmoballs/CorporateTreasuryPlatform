using Moq;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Authentication;

public class UserAdministrationTenantTests
{
    [Fact]
    public async Task AssignRole_ChangesOnlyCurrentMembership()
    {
        var currentOrganizationId =
            Guid.NewGuid();

        var otherOrganizationId =
            Guid.NewGuid();

        var originalGlobalRole =
            CreateRole(
                Roles.TreasuryOfficer);

        var newRole =
            CreateRole(
                Roles.FinanceManager);

        var user =
            CreateUser(
                originalGlobalRole,
                currentOrganizationId,
                otherOrganizationId);

        var currentMembership =
            user.OrganizationMemberships
                .Single(membership =>
                    membership.OrganizationId ==
                        currentOrganizationId);

        var otherMembership =
            user.OrganizationMemberships
                .Single(membership =>
                    membership.OrganizationId ==
                        otherOrganizationId);

        var serviceSetup =
            CreateService(
                user,
                newRole,
                currentOrganizationId);

        var result =
            await serviceSetup.Service.AssignRole(
                user.Id,
                newRole.Id);

        Assert.Equal(
            newRole.Id,
            currentMembership.RoleId);

        Assert.Equal(
            originalGlobalRole.Id,
            otherMembership.RoleId);

        Assert.Equal(
            originalGlobalRole.Id,
            user.RoleId);

        Assert.Equal(
            newRole.Id,
            result.RoleId);

        serviceSetup.UserRepository.Verify(
            repository =>
                repository.SaveChanges(),
            Times.Once);

        serviceSetup.SessionService.Verify(
            service =>
                service
                    .RevokeSessionsForMembership(
                        currentMembership.Id,
                        It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task DeactivateUser_DisablesOnlyCurrentMembership()
    {
        var currentOrganizationId =
            Guid.NewGuid();

        var otherOrganizationId =
            Guid.NewGuid();

        var role =
            CreateRole(
                Roles.TreasuryOfficer);

        var user =
            CreateUser(
                role,
                currentOrganizationId,
                otherOrganizationId);

        var currentMembership =
            user.OrganizationMemberships
                .Single(membership =>
                    membership.OrganizationId ==
                        currentOrganizationId);

        var otherMembership =
            user.OrganizationMemberships
                .Single(membership =>
                    membership.OrganizationId ==
                        otherOrganizationId);

        var serviceSetup =
            CreateService(
                user,
                role,
                currentOrganizationId);

        var result =
            await serviceSetup.Service
                .SetUserStatus(
                    user.Id,
                    isActive: false);

        Assert.False(
            currentMembership.IsActive);

        Assert.True(
            otherMembership.IsActive);

        Assert.True(
            user.IsActive);

        Assert.False(
            result.IsActive);

        serviceSetup.SessionService.Verify(
            service =>
                service
                    .RevokeSessionsForMembership(
                        currentMembership.Id,
                        It.IsAny<string>()),
            Times.Once);
    }

    private static ServiceSetup CreateService(
        User user,
        Role role,
        Guid currentOrganizationId)
    {
        var userRepository =
            new Mock<IUserRepository>();

        userRepository
            .Setup(repository =>
                repository.GetById(user.Id))
            .ReturnsAsync(user);

        var roleRepository =
            new Mock<IRoleRepository>();

        roleRepository
            .Setup(repository =>
                repository.GetById(role.Id))
            .ReturnsAsync(role);

        var currentUserService =
            new Mock<ICurrentUserService>();

        currentUserService
            .SetupGet(service =>
                service.OrganizationId)
            .Returns(
                currentOrganizationId);

        currentUserService
            .SetupGet(service =>
                service.UserId)
            .Returns(
                Guid.NewGuid());

        var sessionService =
            new Mock<
                IAuthenticationSessionService>();

        return new ServiceSetup(
            new UserAdministrationService(
                userRepository.Object,
                roleRepository.Object,
                currentUserService.Object,
                sessionService.Object),
            userRepository,
            sessionService);
    }

    private static User CreateUser(
        Role globalRole,
        Guid currentOrganizationId,
        Guid otherOrganizationId)
    {
        var user =
            new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Tenant",
                LastName = "User",
                Email = "tenant.user@example.com",
                RoleId = globalRole.Id,
                Role = globalRole,
                IsActive = true
            };

        user.OrganizationMemberships.Add(
            CreateMembership(
                user,
                globalRole,
                currentOrganizationId,
                isDefault: false));

        user.OrganizationMemberships.Add(
            CreateMembership(
                user,
                globalRole,
                otherOrganizationId,
                isDefault: true));

        return user;
    }

    private static OrganizationMembership
        CreateMembership(
            User user,
            Role role,
            Guid organizationId,
            bool isDefault)
    {
        return new OrganizationMembership
        {
            Id = Guid.NewGuid(),
            OrganizationId =
                organizationId,
            UserId = user.Id,
            User = user,
            RoleId = role.Id,
            Role = role,
            IsActive = true,
            IsDefault = isDefault
        };
    }

    private static Role CreateRole(
        string name)
    {
        return new Role
        {
            Id = Guid.NewGuid(),
            Name = name
        };
    }

    private sealed record ServiceSetup(
        UserAdministrationService Service,
        Mock<IUserRepository> UserRepository,
        Mock<IAuthenticationSessionService>
            SessionService);
}
