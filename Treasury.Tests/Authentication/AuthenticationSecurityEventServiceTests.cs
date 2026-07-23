using System.Security.Cryptography;
using System.Text;
using Moq;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Authentication;

public class
    AuthenticationSecurityEventServiceTests
{
    private static readonly DateTimeOffset Now =
        new(
            2026,
            7,
            23,
            22,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task
        Record_HashesIdentifierAndCapturesClientContext()
    {
        var repository =
            new Mock<
                IAuthenticationSecurityEventRepository>();

        AuthenticationSecurityEvent? saved = null;

        repository
            .Setup(item => item.Add(
                It.IsAny<
                    AuthenticationSecurityEvent>()))
            .Callback<
                AuthenticationSecurityEvent>(
                item => saved = item)
            .Returns(Task.CompletedTask);

        repository
            .Setup(item => item.SaveChanges())
            .Returns(Task.CompletedTask);

        var client =
            new Mock<IClientRequestContext>();

        client.SetupGet(item => item.IpAddress)
            .Returns("192.0.2.5");

        client.SetupGet(item => item.UserAgent)
            .Returns("Treasury.Tests");

        var service = CreateService(
            repository,
            client.Object);

        await service.Record(
            new RecordAuthenticationSecurityEventDto
            {
                EventType =
                    AuthenticationSecurityEventTypes
                        .LoginFailed,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Failed,
                Identifier =
                    "  USER@example.com ",
                Metadata = new
                {
                    authenticationMethod =
                        AuthenticationMethods.Password
                }
            });

        Assert.NotNull(saved);
        Assert.Equal(
            Hash("USER@EXAMPLE.COM"),
            saved.IdentifierHash);
        Assert.DoesNotContain(
            "USER@EXAMPLE.COM",
            saved.MetadataJson ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("192.0.2.5", saved.IpAddress);
        Assert.Equal(
            "Treasury.Tests",
            saved.UserAgent);
        Assert.Equal(
            Now.UtcDateTime,
            saved.OccurredAtUtc);
    }

    [Fact]
    public async Task
        Record_RejectsSecretBearingMetadata()
    {
        var repository =
            new Mock<
                IAuthenticationSecurityEventRepository>();

        var service = CreateService(
            repository,
            Mock.Of<IClientRequestContext>());

        await Assert.ThrowsAsync<
            ArgumentException>(
            () => service.Record(
                new
                    RecordAuthenticationSecurityEventDto
                    {
                        EventType =
                            AuthenticationSecurityEventTypes
                                .SessionRefreshed,
                        Outcome =
                            AuthenticationSecurityOutcomes
                                .Succeeded,
                        Metadata = new
                        {
                            refreshToken =
                                "must-not-be-recorded"
                        }
                    }));

        repository.Verify(
            item => item.Add(
                It.IsAny<
                    AuthenticationSecurityEvent>()),
            Times.Never);
    }

    [Fact]
    public async Task
        Search_AlwaysScopesToCurrentOrganization()
    {
        var organizationId = Guid.NewGuid();

        var repository =
            new Mock<
                IAuthenticationSecurityEventRepository>();

        repository
            .Setup(item => item.Search(
                organizationId,
                It.IsAny<
                    AuthenticationSecurityEventQueryDto>()))
            .ReturnsAsync((
                Array.Empty<
                    AuthenticationSecurityEvent>(),
                0));

        var currentUser =
            new Mock<ICurrentUserService>();

        currentUser
            .SetupGet(item => item.OrganizationId)
            .Returns(organizationId);

        var service = new
            AuthenticationSecurityEventService(
                repository.Object,
                currentUser.Object,
                Mock.Of<IClientRequestContext>(),
                new FixedTimeProvider(Now));

        var result = await service.Search(
            new AuthenticationSecurityEventQueryDto
            {
                Page = -10,
                PageSize = 1000
            });

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);

        repository.Verify(
            item => item.Search(
                organizationId,
                It.Is<
                    AuthenticationSecurityEventQueryDto>(
                    query =>
                        query.Page == 1 &&
                        query.PageSize == 100)),
            Times.Once);
    }

    private static
        AuthenticationSecurityEventService
        CreateService(
            Mock<
                IAuthenticationSecurityEventRepository>
                repository,
            IClientRequestContext clientContext)
    {
        return new
            AuthenticationSecurityEventService(
                repository.Object,
                Mock.Of<ICurrentUserService>(),
                clientContext,
                new FixedTimeProvider(Now));
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));
    }

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
