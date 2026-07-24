using Microsoft.EntityFrameworkCore;
using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Organizations;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public class OrganizationStructureIntegrationTests
{
    [Fact]
    public async Task
        StructureIsTenantScopedAuditedAndConcurrent()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var now =
            new DateTimeOffset(
                2026,
                7,
                24,
                11,
                0,
                0,
                TimeSpan.Zero);

        SeededData seeded;

        await using (var systemContext =
            database.CreateSystemContext())
        {
            seeded = await SeedForeignTenant(
                systemContext,
                now.UtcDateTime);
        }

        LegalEntityResponseDto createdEntity;
        BusinessUnitResponseDto createdUnit;

        await using (var context =
            database.CreateContext(
                seeded.PrimaryOrganizationId))
        {
            var service = CreateService(
                context,
                seeded.PrimaryOrganizationId,
                now);

            var existing =
                await service.GetLegalEntities();

            Assert.DoesNotContain(
                existing,
                item =>
                    item.Id ==
                        seeded
                            .ForeignLegalEntityId);

            createdEntity =
                await service.CreateLegalEntity(
                    new CreateLegalEntityDto
                    {
                        Code = " shared ",
                        Name =
                            "Primary Shared Entity",
                        CountryCode = "ng",
                        BaseCurrency = "ngn"
                    });

            Assert.Equal(
                "SHARED",
                createdEntity.Code);

            createdUnit =
                await service.CreateBusinessUnit(
                    new CreateBusinessUnitDto
                    {
                        LegalEntityId =
                            createdEntity.Id,
                        Code = " unit-1 ",
                        Name = "Unit One",
                        IsActive = true
                    });

            Assert.Equal(
                createdEntity.Id,
                createdUnit.LegalEntityId);

            await Assert.ThrowsAsync<
                ResourceNotFoundException>(
                () => service.CreateBusinessUnit(
                    new CreateBusinessUnitDto
                    {
                        LegalEntityId =
                            seeded
                                .ForeignLegalEntityId,
                        Code = "FOREIGN-UNIT",
                        Name = "Foreign Unit",
                        IsActive = true
                    }));
        }

        await VerifyConcurrency(
            database,
            seeded.PrimaryOrganizationId,
            createdEntity,
            now);

        await using var verificationContext =
            database.CreateContext(
                seeded.PrimaryOrganizationId);

        Assert.True(
            await verificationContext
                .LegalEntities
                .AnyAsync(item =>
                    item.Id ==
                        createdEntity.Id));

        Assert.True(
            await verificationContext
                .BusinessUnits
                .AnyAsync(item =>
                    item.Id ==
                        createdUnit.Id));

        var auditTypes =
            await verificationContext.AuditLogs
                .AsNoTracking()
                .Where(item =>
                    item.EntityId ==
                        createdEntity.Id ||
                    item.EntityId ==
                        createdUnit.Id)
                .Select(item =>
                    item.EntityType)
                .ToListAsync();

        Assert.Contains(
            AuditEntityTypes.LegalEntity,
            auditTypes);
        Assert.Contains(
            AuditEntityTypes.BusinessUnit,
            auditTypes);
    }

    private static async Task VerifyConcurrency(
        PostgreSqlTestDatabase database,
        Guid organizationId,
        LegalEntityResponseDto entity,
        DateTimeOffset now)
    {
        await using var contextOne =
            database.CreateContext(organizationId);

        await using var contextTwo =
            database.CreateContext(organizationId);

        var repositoryOne =
            new OrganizationStructureRepository(
                contextOne);

        var repositoryTwo =
            new OrganizationStructureRepository(
                contextTwo);

        _ = await repositoryOne
            .GetLegalEntity(entity.Id);

        _ = await repositoryTwo
            .GetLegalEntity(entity.Id);

        var serviceOne = CreateService(
            contextOne,
            organizationId,
            now.AddMinutes(1),
            repositoryOne);

        var serviceTwo = CreateService(
            contextTwo,
            organizationId,
            now.AddMinutes(2),
            repositoryTwo);

        var dto = new UpdateLegalEntityDto
        {
            Name = "First Update",
            CountryCode = "NG",
            BaseCurrency = "NGN",
            ConcurrencyToken =
                entity.ConcurrencyToken
        };

        _ = await serviceOne.UpdateLegalEntity(
            entity.Id,
            dto);

        await Assert.ThrowsAsync<ConflictException>(
            () => serviceTwo.UpdateLegalEntity(
                entity.Id,
                new UpdateLegalEntityDto
                {
                    Name = "Stale Update",
                    CountryCode = "NG",
                    BaseCurrency = "NGN",
                    ConcurrencyToken =
                        entity.ConcurrencyToken
                }));
    }

    private static OrganizationStructureService
        CreateService(
            Treasury.Infrastructure.Persistence
                .TreasuryDbContext context,
            Guid organizationId,
            DateTimeOffset now,
            OrganizationStructureRepository?
                repository = null)
    {
        var currentUser =
            new Mock<ICurrentUserService>();

        currentUser
            .SetupGet(item => item.OrganizationId)
            .Returns(organizationId);

        var auditRepository =
            new AuditLogRepository(context);

        var auditService = new AuditLogService(
            auditRepository,
            currentUser.Object);

        return new OrganizationStructureService(
            repository ??
                new OrganizationStructureRepository(
                    context),
            currentUser.Object,
            auditService,
            new FixedTimeProvider(now));
    }

    private static async Task<SeededData>
        SeedForeignTenant(
            Treasury.Infrastructure.Persistence
                .TreasuryDbContext context,
            DateTime now)
    {
        var primaryOrganization =
            await context.Organizations
                .OrderBy(item =>
                    item.CreatedAtUtc)
                .FirstAsync();

        var suffix =
            Guid.NewGuid()
                .ToString("N")[..8];

        var foreignOrganization =
            new Organization
            {
                Id = Guid.NewGuid(),
                Code =
                    $"FOREIGN-{suffix}"
                        .ToUpperInvariant(),
                Name = "Foreign Organization",
                Slug = $"foreign-{suffix}",
                CountryCode = "NG",
                BaseCurrency = "NGN",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

        var foreignEntity =
            new LegalEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    foreignOrganization.Id,
                Organization =
                    foreignOrganization,
                Code = "SHARED",
                Name = "Foreign Shared Entity",
                CountryCode = "NG",
                BaseCurrency = "NGN",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

        await context.Organizations.AddAsync(
            foreignOrganization);

        await context.LegalEntities.AddAsync(
            foreignEntity);

        await context.SaveChangesAsync();

        return new SeededData(
            primaryOrganization.Id,
            foreignEntity.Id);
    }

    private sealed record SeededData(
        Guid PrimaryOrganizationId,
        Guid ForeignLegalEntityId);

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
