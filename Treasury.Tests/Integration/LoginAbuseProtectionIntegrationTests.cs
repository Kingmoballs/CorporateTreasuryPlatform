using Microsoft.EntityFrameworkCore;
using Moq;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Repositories;

namespace Treasury.Tests.Integration;

public class LoginAbuseProtectionIntegrationTests
{
    [Fact]
    public async Task
        ConcurrentFailuresLockAccountAndSuccessfulLoginWaitsForExpiry()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var now =
            new DateTime(
                2026,
                7,
                23,
                20,
                30,
                0,
                DateTimeKind.Utc);

        var userId = Guid.NewGuid();

        await using (var seedContext =
            database.CreateSystemContext())
        {
            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name =
                    "Login Protection Test " +
                    Guid.NewGuid().ToString("N")
            };

            await seedContext.Users.AddAsync(
                new User
                {
                    Id = userId,
                    FirstName = "Login",
                    LastName = "Tester",
                    Email =
                        $"{Guid.NewGuid():N}" +
                        "@example.com",
                    PasswordHash = "not-used",
                    EmailVerifiedAtUtc =
                        now.AddDays(-1),
                    RoleId = role.Id,
                    Role = role,
                    IsActive = true
                });

            await seedContext.SaveChangesAsync();
        }

        var contexts =
            Enumerable.Range(0, 5)
                .Select(_ =>
                    database.CreateSystemContext())
                .ToList();

        try
        {
            var repositories =
                contexts
                    .Select(context =>
                        new UserRepository(
                            context,
                            CreateSystemScope()))
                    .ToList();

            await Task.WhenAll(
                repositories.Select(repository =>
                    repository.RecordFailedLogin(
                        userId,
                        now,
                        now.AddMinutes(-15),
                        maximumFailedAttempts: 5,
                        now.AddMinutes(15))));
        }
        finally
        {
            foreach (var context in contexts)
            {
                await context.DisposeAsync();
            }
        }

        await using (var verificationContext =
            database.CreateSystemContext())
        {
            var storedUser =
                await verificationContext.Users
                    .AsNoTracking()
                    .SingleAsync(user =>
                        user.Id == userId);

            Assert.Equal(
                5,
                storedUser.FailedLoginAttempts);
            Assert.Equal(
                now,
                storedUser
                    .LoginFailureWindowStartedAtUtc);
            Assert.Equal(
                now,
                storedUser.LastFailedLoginAtUtc);
            Assert.Equal(
                now.AddMinutes(15),
                storedUser.LoginLockoutEndUtc);
        }

        await using (var lockedContext =
            database.CreateSystemContext())
        {
            var repository =
                new UserRepository(
                    lockedContext,
                    CreateSystemScope());

            var cleared =
                await repository
                    .ClearFailedLoginsIfNotLocked(
                        userId,
                        now.AddMinutes(5));

            Assert.False(cleared);
        }

        await using (var expiredContext =
            database.CreateSystemContext())
        {
            var repository =
                new UserRepository(
                    expiredContext,
                    CreateSystemScope());

            var cleared =
                await repository
                    .ClearFailedLoginsIfNotLocked(
                        userId,
                        now.AddMinutes(16));

            Assert.True(cleared);
        }

        await using var finalContext =
            database.CreateSystemContext();

        var clearedUser =
            await finalContext.Users
                .AsNoTracking()
                .SingleAsync(user =>
                    user.Id == userId);

        Assert.Equal(
            0,
            clearedUser.FailedLoginAttempts);
        Assert.Null(
            clearedUser
                .LoginFailureWindowStartedAtUtc);
        Assert.Null(
            clearedUser.LastFailedLoginAtUtc);
        Assert.Null(
            clearedUser.LoginLockoutEndUtc);
    }

    private static IOrganizationContext
        CreateSystemScope()
    {
        var context =
            new Mock<IOrganizationContext>();

        context
            .SetupGet(item => item.IsSystemScope)
            .Returns(true);

        return context.Object;
    }
}
