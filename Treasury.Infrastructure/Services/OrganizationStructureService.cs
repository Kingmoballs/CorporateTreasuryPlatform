using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.Organizations;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class OrganizationStructureService
    : IOrganizationStructureService
{
    private readonly IOrganizationStructureRepository
        _repository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuditLogService
        _auditLogService;

    private readonly TimeProvider _timeProvider;

    public OrganizationStructureService(
        IOrganizationStructureRepository repository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUserService =
            currentUserService;
        _auditLogService = auditLogService;
        _timeProvider = timeProvider;
    }

    public async Task<
        OrganizationProfileResponseDto>
        GetOrganization()
    {
        return Map(
            await GetRequiredOrganization());
    }

    public async Task<
        OrganizationProfileResponseDto>
        UpdateOrganization(
            UpdateOrganizationProfileDto dto)
    {
        var organization =
            await GetRequiredOrganization();

        EnsureExpectedToken(
            organization.ConcurrencyToken,
            dto.ConcurrencyToken,
            "organization");

        var before = Snapshot(organization);

        organization.Name =
            NormalizeRequiredText(
                dto.Name,
                "Organization name",
                200);

        organization.CountryCode =
            NormalizeCountryCode(
                dto.CountryCode);

        organization.BaseCurrency =
            NormalizeCurrency(
                dto.BaseCurrency);

        organization.UpdatedAtUtc = GetUtcNow();
        organization.ConcurrencyToken =
            Guid.NewGuid();

        _repository.SetOriginalConcurrencyToken(
            organization,
            dto.ConcurrencyToken);

        await SaveUpdate("organization");

        await RecordAudit(
            AuditActionTypes.Updated,
            AuditEntityTypes.Organization,
            organization.Id,
            organization.Code,
            $"Organization {organization.Code} " +
            "was updated.",
            before,
            Snapshot(organization));

        return Map(organization);
    }

    public async Task<IReadOnlyList<
        LegalEntityResponseDto>>
        GetLegalEntities()
    {
        var entities =
            await _repository.GetLegalEntities();

        return entities.Select(Map).ToList();
    }

    public async Task<LegalEntityResponseDto>
        CreateLegalEntity(
            CreateLegalEntityDto dto)
    {
        var code = NormalizeCode(
            dto.Code,
            "Legal entity code");

        if (await _repository
                .LegalEntityCodeExists(code))
        {
            throw new ConflictException(
                $"A legal entity with code {code} " +
                "already exists.");
        }

        var now = GetUtcNow();
        var entity = new LegalEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId =
                RequireOrganizationId(),
            Code = code,
            Name = NormalizeRequiredText(
                dto.Name,
                "Legal entity name",
                200),
            RegistrationNumber =
                NormalizeOptionalText(
                    dto.RegistrationNumber,
                    "Registration number",
                    100),
            TaxIdentificationNumber =
                NormalizeOptionalText(
                    dto.TaxIdentificationNumber,
                    "Tax identification number",
                    100),
            CountryCode =
                NormalizeCountryCode(
                    dto.CountryCode),
            BaseCurrency =
                NormalizeCurrency(
                    dto.BaseCurrency),
            IsActive = dto.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        await _repository.AddLegalEntity(entity);
        await SaveCreate("legal entity");

        await RecordAudit(
            AuditActionTypes.Created,
            AuditEntityTypes.LegalEntity,
            entity.Id,
            entity.Code,
            $"Legal entity {entity.Code} was " +
            "created.",
            null,
            Snapshot(entity));

        return Map(entity);
    }

    public async Task<LegalEntityResponseDto>
        UpdateLegalEntity(
            Guid id,
            UpdateLegalEntityDto dto)
    {
        var entity =
            await GetRequiredLegalEntity(id);

        EnsureExpectedToken(
            entity.ConcurrencyToken,
            dto.ConcurrencyToken,
            "legal entity");

        var before = Snapshot(entity);

        entity.Name =
            NormalizeRequiredText(
                dto.Name,
                "Legal entity name",
                200);

        entity.RegistrationNumber =
            NormalizeOptionalText(
                dto.RegistrationNumber,
                "Registration number",
                100);

        entity.TaxIdentificationNumber =
            NormalizeOptionalText(
                dto.TaxIdentificationNumber,
                "Tax identification number",
                100);

        entity.CountryCode =
            NormalizeCountryCode(
                dto.CountryCode);

        entity.BaseCurrency =
            NormalizeCurrency(
                dto.BaseCurrency);

        PrepareUpdate(
            entity,
            dto.ConcurrencyToken);

        await SaveUpdate("legal entity");

        await RecordAudit(
            AuditActionTypes.Updated,
            AuditEntityTypes.LegalEntity,
            entity.Id,
            entity.Code,
            $"Legal entity {entity.Code} was " +
            "updated.",
            before,
            Snapshot(entity));

        return Map(entity);
    }

    public async Task<LegalEntityResponseDto>
        SetLegalEntityStatus(
            Guid id,
            UpdateOrganizationStructureStatusDto dto)
    {
        var entity =
            await GetRequiredLegalEntity(id);

        EnsureExpectedToken(
            entity.ConcurrencyToken,
            dto.ConcurrencyToken,
            "legal entity");

        if (entity.IsActive == dto.IsActive)
        {
            return Map(entity);
        }

        if (!dto.IsActive &&
            await _repository
                .HasActiveBusinessUnits(entity.Id))
        {
            throw new BusinessRuleException(
                "Deactivate the legal entity's active " +
                "business units first.");
        }

        var before = Snapshot(entity);
        entity.IsActive = dto.IsActive;

        PrepareUpdate(
            entity,
            dto.ConcurrencyToken);

        await SaveUpdate("legal entity");

        await RecordAudit(
            dto.IsActive
                ? AuditActionTypes.Activated
                : AuditActionTypes.Suspended,
            AuditEntityTypes.LegalEntity,
            entity.Id,
            entity.Code,
            dto.IsActive
                ? $"Legal entity {entity.Code} was " +
                  "activated."
                : $"Legal entity {entity.Code} was " +
                  "deactivated.",
            before,
            Snapshot(entity));

        return Map(entity);
    }

    public async Task<IReadOnlyList<
        BusinessUnitResponseDto>>
        GetBusinessUnits(Guid? legalEntityId)
    {
        var units =
            await _repository.GetBusinessUnits(
                legalEntityId);

        return units.Select(Map).ToList();
    }

    public async Task<BusinessUnitResponseDto>
        CreateBusinessUnit(
            CreateBusinessUnitDto dto)
    {
        var legalEntity =
            await GetRequiredLegalEntity(
                dto.LegalEntityId);

        if (dto.IsActive &&
            !legalEntity.IsActive)
        {
            throw new BusinessRuleException(
                "An active business unit requires an " +
                "active legal entity.");
        }

        var code = NormalizeCode(
            dto.Code,
            "Business unit code");

        if (await _repository
                .BusinessUnitCodeExists(code))
        {
            throw new ConflictException(
                $"A business unit with code {code} " +
                "already exists.");
        }

        var now = GetUtcNow();
        var unit = new BusinessUnit
        {
            Id = Guid.NewGuid(),
            OrganizationId =
                RequireOrganizationId(),
            LegalEntityId = legalEntity.Id,
            LegalEntity = legalEntity,
            Code = code,
            Name = NormalizeRequiredText(
                dto.Name,
                "Business unit name",
                200),
            IsActive = dto.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        await _repository.AddBusinessUnit(unit);
        await SaveCreate("business unit");

        await RecordAudit(
            AuditActionTypes.Created,
            AuditEntityTypes.BusinessUnit,
            unit.Id,
            unit.Code,
            $"Business unit {unit.Code} was " +
            "created.",
            null,
            Snapshot(unit));

        return Map(unit);
    }

    public async Task<BusinessUnitResponseDto>
        UpdateBusinessUnit(
            Guid id,
            UpdateBusinessUnitDto dto)
    {
        var unit =
            await GetRequiredBusinessUnit(id);

        EnsureExpectedToken(
            unit.ConcurrencyToken,
            dto.ConcurrencyToken,
            "business unit");

        var legalEntity =
            await GetRequiredLegalEntity(
                dto.LegalEntityId);

        if (unit.IsActive &&
            !legalEntity.IsActive)
        {
            throw new BusinessRuleException(
                "An active business unit requires an " +
                "active legal entity.");
        }

        var before = Snapshot(unit);

        unit.LegalEntityId = legalEntity.Id;
        unit.LegalEntity = legalEntity;
        unit.Name = NormalizeRequiredText(
            dto.Name,
            "Business unit name",
            200);

        PrepareUpdate(
            unit,
            dto.ConcurrencyToken);

        await SaveUpdate("business unit");

        await RecordAudit(
            AuditActionTypes.Updated,
            AuditEntityTypes.BusinessUnit,
            unit.Id,
            unit.Code,
            $"Business unit {unit.Code} was " +
            "updated.",
            before,
            Snapshot(unit));

        return Map(unit);
    }

    public async Task<BusinessUnitResponseDto>
        SetBusinessUnitStatus(
            Guid id,
            UpdateOrganizationStructureStatusDto dto)
    {
        var unit =
            await GetRequiredBusinessUnit(id);

        EnsureExpectedToken(
            unit.ConcurrencyToken,
            dto.ConcurrencyToken,
            "business unit");

        if (unit.IsActive == dto.IsActive)
        {
            return Map(unit);
        }

        if (dto.IsActive &&
            !unit.LegalEntity.IsActive)
        {
            throw new BusinessRuleException(
                "Activate the legal entity before " +
                "activating this business unit.");
        }

        var before = Snapshot(unit);
        unit.IsActive = dto.IsActive;

        PrepareUpdate(
            unit,
            dto.ConcurrencyToken);

        await SaveUpdate("business unit");

        await RecordAudit(
            dto.IsActive
                ? AuditActionTypes.Activated
                : AuditActionTypes.Suspended,
            AuditEntityTypes.BusinessUnit,
            unit.Id,
            unit.Code,
            dto.IsActive
                ? $"Business unit {unit.Code} was " +
                  "activated."
                : $"Business unit {unit.Code} was " +
                  "deactivated.",
            before,
            Snapshot(unit));

        return Map(unit);
    }

    private async Task<Organization>
        GetRequiredOrganization()
    {
        var organization =
            await _repository.GetOrganization(
                RequireOrganizationId());

        if (organization is null)
        {
            throw new ResourceNotFoundException(
                "Organization was not found.");
        }

        return organization;
    }

    private async Task<LegalEntity>
        GetRequiredLegalEntity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Legal entity ID is required.");
        }

        var entity =
            await _repository.GetLegalEntity(id);

        if (entity is null)
        {
            throw new ResourceNotFoundException(
                "Legal entity was not found.");
        }

        return entity;
    }

    private async Task<BusinessUnit>
        GetRequiredBusinessUnit(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Business unit ID is required.");
        }

        var unit =
            await _repository.GetBusinessUnit(id);

        if (unit is null)
        {
            throw new ResourceNotFoundException(
                "Business unit was not found.");
        }

        return unit;
    }

    private void PrepareUpdate(
        LegalEntity entity,
        Guid expectedToken)
    {
        entity.UpdatedAtUtc = GetUtcNow();
        entity.ConcurrencyToken = Guid.NewGuid();

        _repository.SetOriginalConcurrencyToken(
            entity,
            expectedToken);
    }

    private void PrepareUpdate(
        BusinessUnit unit,
        Guid expectedToken)
    {
        unit.UpdatedAtUtc = GetUtcNow();
        unit.ConcurrencyToken = Guid.NewGuid();

        _repository.SetOriginalConcurrencyToken(
            unit,
            expectedToken);
    }

    private async Task SaveCreate(
        string resourceName)
    {
        try
        {
            await _repository.SaveChanges();
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                $"The {resourceName} could not be " +
                "created. Its code may already be in " +
                "use.");
        }
    }

    private async Task SaveUpdate(
        string resourceName)
    {
        try
        {
            await _repository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                $"The {resourceName} changed in another " +
                "request. Reload it and try again.");
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                $"The {resourceName} update could not " +
                "be completed.");
        }
    }

    private Task RecordAudit(
        string action,
        string entityType,
        Guid entityId,
        string entityReference,
        string summary,
        object? before,
        object? after)
    {
        return _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                EntityReference =
                    entityReference,
                Summary = summary,
                BeforeValues = before,
                AfterValues = after,
                Metadata = new
                {
                    Module =
                        "Organization Administration"
                }
            });
    }

    private Guid RequireOrganizationId()
    {
        var organizationId =
            _currentUserService.OrganizationId;

        if (!organizationId.HasValue ||
            organizationId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "A valid organization context is " +
                "required.");
        }

        return organizationId.Value;
    }

    private static void EnsureExpectedToken(
        Guid currentToken,
        Guid expectedToken,
        string resourceName)
    {
        if (expectedToken == Guid.Empty ||
            currentToken != expectedToken)
        {
            throw new ConflictException(
                $"The {resourceName} changed in another " +
                "request. Reload it and try again.");
        }
    }

    private static string NormalizeCode(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                $"{fieldName} is required.");
        }

        var code =
            value.Trim().ToUpperInvariant();

        if (!Regex.IsMatch(
                code,
                "^[A-Z0-9][A-Z0-9-]{0,49}$"))
        {
            throw new BusinessRuleException(
                $"{fieldName} can contain only letters, " +
                "numbers and hyphens, and cannot exceed " +
                "50 characters.");
        }

        return code;
    }

    private static string NormalizeCountryCode(
        string? value)
    {
        var code = value?.Trim().ToUpperInvariant();

        if (code is null ||
            !Regex.IsMatch(code, "^[A-Z]{2}$"))
        {
            throw new BusinessRuleException(
                "Country code must contain exactly two " +
                "letters.");
        }

        return code;
    }

    private static string NormalizeCurrency(
        string? value)
    {
        var currency =
            value?.Trim().ToUpperInvariant();

        if (currency is null ||
            !Regex.IsMatch(currency, "^[A-Z]{3}$"))
        {
            throw new BusinessRuleException(
                "Base currency must contain exactly " +
                "three letters.");
        }

        return currency;
    }

    private static string NormalizeRequiredText(
        string? value,
        string fieldName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                $"{fieldName} is required.");
        }

        var normalized = value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new BusinessRuleException(
                $"{fieldName} cannot exceed " +
                $"{maximumLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(
        string? value,
        string fieldName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new BusinessRuleException(
                $"{fieldName} cannot exceed " +
                $"{maximumLength} characters.");
        }

        return normalized;
    }

    private static OrganizationProfileResponseDto
        Map(Organization organization)
    {
        return new OrganizationProfileResponseDto
        {
            Id = organization.Id,
            Code = organization.Code,
            Name = organization.Name,
            Slug = organization.Slug,
            CountryCode =
                organization.CountryCode,
            BaseCurrency =
                organization.BaseCurrency,
            IsActive = organization.IsActive,
            CreatedAtUtc =
                organization.CreatedAtUtc,
            UpdatedAtUtc =
                organization.UpdatedAtUtc,
            ConcurrencyToken =
                organization.ConcurrencyToken
        };
    }

    private static LegalEntityResponseDto Map(
        LegalEntity entity)
    {
        return new LegalEntityResponseDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            RegistrationNumber =
                entity.RegistrationNumber,
            TaxIdentificationNumber =
                entity.TaxIdentificationNumber,
            CountryCode = entity.CountryCode,
            BaseCurrency = entity.BaseCurrency,
            IsActive = entity.IsActive,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            ConcurrencyToken =
                entity.ConcurrencyToken
        };
    }

    private static BusinessUnitResponseDto Map(
        BusinessUnit unit)
    {
        return new BusinessUnitResponseDto
        {
            Id = unit.Id,
            LegalEntityId = unit.LegalEntityId,
            LegalEntityCode =
                unit.LegalEntity.Code,
            Code = unit.Code,
            Name = unit.Name,
            IsActive = unit.IsActive,
            CreatedAtUtc = unit.CreatedAtUtc,
            UpdatedAtUtc = unit.UpdatedAtUtc,
            ConcurrencyToken =
                unit.ConcurrencyToken
        };
    }

    private static object Snapshot(
        Organization organization)
    {
        return new
        {
            organization.Id,
            organization.Code,
            organization.Name,
            organization.Slug,
            organization.CountryCode,
            organization.BaseCurrency,
            organization.IsActive,
            organization.CreatedAtUtc,
            organization.UpdatedAtUtc
        };
    }

    private static object Snapshot(
        LegalEntity entity)
    {
        return new
        {
            entity.Id,
            entity.Code,
            entity.Name,
            entity.RegistrationNumber,
            entity.TaxIdentificationNumber,
            entity.CountryCode,
            entity.BaseCurrency,
            entity.IsActive,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc
        };
    }

    private static object Snapshot(
        BusinessUnit unit)
    {
        return new
        {
            unit.Id,
            unit.LegalEntityId,
            unit.Code,
            unit.Name,
            unit.IsActive,
            unit.CreatedAtUtc,
            unit.UpdatedAtUtc
        };
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider
            .GetUtcNow()
            .UtcDateTime;
    }
}
