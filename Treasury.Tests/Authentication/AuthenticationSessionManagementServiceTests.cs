using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;

namespace Treasury.Tests.Authentication;

public class
    AuthenticationSessionManagementServiceTests
{
    private static readonly DateTimeOffset Now =
        new(
            2026,
            7,
            23,
            22,
            30,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task
        GetActiveSessions_MarksOnlyCurrentSession()
    {
        var setup = CreateSetup();
        var otherSessionId = Guid.NewGuid();

        setup.Repository
            .Setup(item =>
                item.GetActiveSessionsForUser(
                    setup.UserId,
                    Now.UtcDateTime))
            .ReturnsAsync(
                new[]
                {
                    CreateSession(
                        setup,
                        setup.CurrentSessionId),
                    CreateSession(
                        setup,
                        otherSessionId)
                });

        var sessions =
            await setup.Service.GetActiveSessions();

        Assert.Equal(2, sessions.Count);
        Assert.True(
            sessions.Single(item =>
                item.Id ==
                    setup.CurrentSessionId)
                .IsCurrent);
        Assert.False(
            sessions.Single(item =>
                item.Id == otherSessionId)
                .IsCurrent);
    }

    [Fact]
    public async Task
        RevokeOwnedSession_DoesNotRevokeUnknownSession()
    {
        var setup = CreateSetup();

        setup.Repository
            .Setup(item =>
                item.GetActiveSessionsForUser(
                    setup.UserId,
                    Now.UtcDateTime))
            .ReturnsAsync(
                Array.Empty<
                    AuthenticationSession>());

        await Assert.ThrowsAsync<
            ResourceNotFoundException>(
            () => setup.Service.RevokeOwnedSession(
                Guid.NewGuid()));

        setup.Repository.Verify(
            item => item.RevokeOwnedSession(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task
        RevokeOtherSessions_PreservesCurrentSession()
    {
        var setup = CreateSetup();

        setup.Repository
            .Setup(item => item.RevokeOtherSessions(
                setup.UserId,
                setup.CurrentSessionId,
                Now.UtcDateTime,
                It.IsAny<string>()))
            .ReturnsAsync(2);

        await setup.Service.RevokeOtherSessions();

        setup.Repository.Verify(
            item => item.RevokeOtherSessions(
                setup.UserId,
                setup.CurrentSessionId,
                Now.UtcDateTime,
                It.IsAny<string>()),
            Times.Once);

        setup.SecurityEvents.Verify(
            item => item.Record(
                It.Is<
                    RecordAuthenticationSecurityEventDto>(
                    dto =>
                        dto.AuthenticationSessionId ==
                            setup.CurrentSessionId)),
            Times.Once);
    }

    private static ServiceSetup CreateSetup()
    {
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var currentSessionId = Guid.NewGuid();

        var repository =
            new Mock<
                IAuthenticationSessionRepository>();

        var currentUser =
            new Mock<ICurrentUserService>();

        currentUser.SetupGet(item => item.UserId)
            .Returns(userId);

        currentUser
            .SetupGet(item => item.OrganizationId)
            .Returns(organizationId);

        currentUser
            .SetupGet(item =>
                item.AuthenticationSessionId)
            .Returns(currentSessionId);

        var securityEvents =
            new Mock<
                IAuthenticationSecurityEventService>();

        securityEvents
            .Setup(item => item.Record(
                It.IsAny<
                    RecordAuthenticationSecurityEventDto>()))
            .Returns(Task.CompletedTask);

        var service = new
            AuthenticationSessionManagementService(
                repository.Object,
                currentUser.Object,
                securityEvents.Object,
                new FixedTimeProvider(Now));

        return new ServiceSetup(
            service,
            repository,
            securityEvents,
            userId,
            organizationId,
            currentSessionId);
    }

    private static AuthenticationSession
        CreateSession(
            ServiceSetup setup,
            Guid id)
    {
        return new AuthenticationSession
        {
            Id = id,
            UserId = setup.UserId,
            OrganizationId =
                setup.OrganizationId,
            Organization = new Organization
            {
                Id = setup.OrganizationId,
                Code = "ORG"
            },
            AuthenticationMethod = "password",
            CreatedAtUtc =
                Now.AddHours(-1).UtcDateTime,
            LastActivityAtUtc =
                Now.UtcDateTime,
            ExpiresAtUtc =
                Now.AddDays(1).UtcDateTime
        };
    }

    private sealed record ServiceSetup(
        AuthenticationSessionManagementService
            Service,
        Mock<IAuthenticationSessionRepository>
            Repository,
        Mock<IAuthenticationSecurityEventService>
            SecurityEvents,
        Guid UserId,
        Guid OrganizationId,
        Guid CurrentSessionId);

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(
            DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset
            GetUtcNow()
        {
            return _now;
        }
    }
}
