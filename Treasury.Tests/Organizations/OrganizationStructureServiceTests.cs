using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.Organizations;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Organizations;

public class OrganizationStructureServiceTests
{
    private static readonly DateTimeOffset Now =
        new(
            2026,
            7,
            24,
            10,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task
        CreateLegalEntity_NormalizesAndScopesRecord()
    {
        var setup = CreateSetup();
        LegalEntity? added = null;

        setup.Repository
            .Setup(item =>
                item.LegalEntityCodeExists("LAGOS-1"))
            .ReturnsAsync(false);

        setup.Repository
            .Setup(item => item.AddLegalEntity(
                It.IsAny<LegalEntity>()))
            .Callback<LegalEntity>(
                item => added = item)
            .Returns(Task.CompletedTask);

        setup.Repository
            .Setup(item => item.SaveChanges())
            .Returns(Task.CompletedTask);

        var result =
            await setup.Service.CreateLegalEntity(
                new CreateLegalEntityDto
                {
                    Code = " lagos-1 ",
                    Name = " Lagos Operations ",
                    CountryCode = "ng",
                    BaseCurrency = "ngn"
                });

        Assert.NotNull(added);
        Assert.Equal(
            setup.OrganizationId,
            added.OrganizationId);
        Assert.Equal("LAGOS-1", result.Code);
        Assert.Equal("NG", result.CountryCode);
        Assert.Equal("NGN", result.BaseCurrency);

        setup.AuditLogs.Verify(
            item => item.Record(
                It.Is<CreateAuditLogDto>(
                    dto =>
                        dto.EntityType ==
                            AuditEntityTypes
                                .LegalEntity &&
                        dto.Action ==
                            AuditActionTypes.Created)),
            Times.Once);
    }

    [Fact]
    public async Task
        DeactivateLegalEntity_WithActiveUnitsIsRejected()
    {
        var setup = CreateSetup();
        var entity = CreateLegalEntity(
            setup.OrganizationId,
            isActive: true);

        setup.Repository
            .Setup(item =>
                item.GetLegalEntity(entity.Id))
            .ReturnsAsync(entity);

        setup.Repository
            .Setup(item =>
                item.HasActiveBusinessUnits(
                    entity.Id))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<
            BusinessRuleException>(
            () => setup.Service
                .SetLegalEntityStatus(
                    entity.Id,
                    new
                        UpdateOrganizationStructureStatusDto
                        {
                            IsActive = false,
                            ConcurrencyToken =
                                entity
                                    .ConcurrencyToken
                        }));

        setup.Repository.Verify(
            item => item.SaveChanges(),
            Times.Never);
    }

    [Fact]
    public async Task
        UpdateLegalEntity_StaleTokenIsRejected()
    {
        var setup = CreateSetup();
        var entity = CreateLegalEntity(
            setup.OrganizationId,
            isActive: true);

        setup.Repository
            .Setup(item =>
                item.GetLegalEntity(entity.Id))
            .ReturnsAsync(entity);

        await Assert.ThrowsAsync<
            ConflictException>(
            () => setup.Service.UpdateLegalEntity(
                entity.Id,
                new UpdateLegalEntityDto
                {
                    Name = "Updated",
                    CountryCode = "NG",
                    BaseCurrency = "NGN",
                    ConcurrencyToken =
                        Guid.NewGuid()
                }));

        setup.Repository.Verify(
            item => item.SaveChanges(),
            Times.Never);
    }

    [Fact]
    public async Task
        CreateActiveBusinessUnit_RequiresActiveParent()
    {
        var setup = CreateSetup();
        var entity = CreateLegalEntity(
            setup.OrganizationId,
            isActive: false);

        setup.Repository
            .Setup(item =>
                item.GetLegalEntity(entity.Id))
            .ReturnsAsync(entity);

        await Assert.ThrowsAsync<
            BusinessRuleException>(
            () => setup.Service.CreateBusinessUnit(
                new CreateBusinessUnitDto
                {
                    LegalEntityId = entity.Id,
                    Code = "UNIT-1",
                    Name = "Unit One",
                    IsActive = true
                }));

        setup.Repository.Verify(
            item => item.AddBusinessUnit(
                It.IsAny<BusinessUnit>()),
            Times.Never);
    }

    private static ServiceSetup CreateSetup()
    {
        var organizationId = Guid.NewGuid();

        var repository =
            new Mock<
                IOrganizationStructureRepository>();

        var currentUser =
            new Mock<ICurrentUserService>();

        currentUser
            .SetupGet(item => item.OrganizationId)
            .Returns(organizationId);

        var auditLogs =
            new Mock<IAuditLogService>();

        auditLogs
            .Setup(item => item.Record(
                It.IsAny<CreateAuditLogDto>()))
            .Returns(Task.CompletedTask);

        var service =
            new OrganizationStructureService(
                repository.Object,
                currentUser.Object,
                auditLogs.Object,
                new FixedTimeProvider(Now));

        return new ServiceSetup(
            service,
            repository,
            auditLogs,
            organizationId);
    }

    private static LegalEntity CreateLegalEntity(
        Guid organizationId,
        bool isActive)
    {
        return new LegalEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Code = "ENTITY-1",
            Name = "Entity One",
            CountryCode = "NG",
            BaseCurrency = "NGN",
            IsActive = isActive,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    private sealed record ServiceSetup(
        OrganizationStructureService Service,
        Mock<IOrganizationStructureRepository>
            Repository,
        Mock<IAuditLogService> AuditLogs,
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

        public override DateTimeOffset
            GetUtcNow()
        {
            return _now;
        }
    }
}
