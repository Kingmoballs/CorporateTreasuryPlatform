using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.OrganizationOnboarding;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class OrganizationOnboardingService
    : IOrganizationOnboardingService
{
    private static readonly JsonSerializerOptions
        JsonOptions = new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

    private static readonly string[]
        DefaultApprovalOperationTypes =
        {
            ApprovalOperationTypes.InternalTransfer,
            ApprovalOperationTypes.CashPayment,
            ApprovalOperationTypes.TransactionReversal,
            ApprovalOperationTypes.InvestmentPlacement,
            ApprovalOperationTypes
                .InvestmentEarlyRedemption,
            ApprovalOperationTypes.InvestmentRollover,
            ApprovalOperationTypes
                .CreditFacilityActivation
        };

    private readonly IOrganizationApplicationRepository
        _repository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IClientRequestContext
        _clientRequestContext;

    private readonly IEmailSender _emailSender;

    private readonly UserInvitationOptions
        _invitationOptions;

    private readonly OrganizationOnboardingOptions
        _onboardingOptions;

    private readonly TimeProvider _timeProvider;

    private readonly ILogger<
        OrganizationOnboardingService> _logger;

    public OrganizationOnboardingService(
        IOrganizationApplicationRepository repository,
        ICurrentUserService currentUserService,
        IClientRequestContext clientRequestContext,
        IEmailSender emailSender,
        IOptions<UserInvitationOptions>
            invitationOptions,
        IOptions<OrganizationOnboardingOptions>
            onboardingOptions,
        TimeProvider timeProvider,
        ILogger<OrganizationOnboardingService> logger)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _clientRequestContext =
            clientRequestContext;
        _emailSender = emailSender;
        _invitationOptions =
            invitationOptions.Value;
        _onboardingOptions =
            onboardingOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<
        OrganizationApplicationResponseDto> Submit(
            SubmitOrganizationApplicationDto dto,
            Guid submissionKey)
    {
        if (submissionKey == Guid.Empty)
        {
            throw new RequestValidationException(
                "A valid Idempotency-Key header is " +
                "required.");
        }

        var normalized =
            NormalizeSubmission(dto);

        var existing =
            await _repository
                .GetBySubmissionKey(
                    submissionKey);

        if (existing is not null)
        {
            if (!Matches(existing, normalized))
            {
                throw new ConflictException(
                    "The Idempotency-Key has already " +
                    "been used for a different " +
                    "organization application.");
            }

            var replay = Map(existing);
            replay.IsIdempotentReplay = true;

            return replay;
        }

        if (await _repository.HasOpenApplication(
                normalized
                    .NormalizedOrganizationName,
                normalized.AdminEmail))
        {
            throw new ConflictException(
                "An open application already exists " +
                "for this organization and admin email.");
        }

        /*
         * Verify that the installation has initialized the
         * platform tenant. The application row itself is the
         * durable submission record. Organization-scoped
         * audit entries begin when an authenticated
         * PlatformAdmin starts the review.
         */
        _ = await GetRequiredPlatformOrganization();

        var application =
            new OrganizationApplication
            {
                Id = Guid.NewGuid(),
                SubmissionKey = submissionKey,
                OrganizationName =
                    normalized.OrganizationName,
                NormalizedOrganizationName =
                    normalized
                        .NormalizedOrganizationName,
                RegistrationNumber =
                    normalized.RegistrationNumber,
                TaxIdentificationNumber =
                    normalized
                        .TaxIdentificationNumber,
                CountryCode =
                    normalized.CountryCode,
                BaseCurrency =
                    normalized.BaseCurrency,
                AdminFirstName =
                    normalized.AdminFirstName,
                AdminLastName =
                    normalized.AdminLastName,
                AdminEmail =
                    normalized.AdminEmail,
                ContactPhoneNumber =
                    normalized.ContactPhoneNumber,
                ApplicationNotes =
                    normalized.ApplicationNotes,
                Status =
                    OrganizationApplicationStatuses
                        .Submitted,
                SubmittedAtUtc = GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            };

        await _repository.AddApplication(
            application);

        try
        {
            await _repository.SaveChanges();
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The application could not be submitted. " +
                "Retry with the same Idempotency-Key.");
        }

        return Map(application);
    }

    public async Task<
        PagedOrganizationApplicationsDto> Search(
            OrganizationApplicationQueryDto query)
    {
        await RequirePlatformAdmin();

        query.Page = Math.Max(1, query.Page);
        query.PageSize =
            Math.Clamp(query.PageSize, 1, 100);

        if (!string.IsNullOrWhiteSpace(
                query.Status))
        {
            query.Status =
                NormalizeStatus(query.Status);
        }

        query.Search =
            NormalizeOptional(
                query.Search,
                maximumLength: 200);

        var result =
            await _repository.Search(query);

        return new PagedOrganizationApplicationsDto
        {
            Items =
                result.Items
                    .Select(Map)
                    .ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = result.TotalCount,
            TotalPages =
                (int)Math.Ceiling(
                    result.TotalCount /
                    (double)query.PageSize)
        };
    }

    public async Task<
        OrganizationApplicationResponseDto> GetById(
            Guid applicationId)
    {
        await RequirePlatformAdmin();

        return Map(
            await GetRequiredApplication(
                applicationId));
    }

    public async Task<
        OrganizationApplicationResponseDto>
        BeginReview(
            Guid applicationId,
            ReviewOrganizationApplicationDto dto)
    {
        var platformOrganization =
            await RequirePlatformAdmin();

        var application =
            await GetRequiredApplication(
                applicationId);

        if (application.Status ==
            OrganizationApplicationStatuses
                .UnderReview)
        {
            EnsureAssignedReviewer(application);
            return Map(application);
        }

        EnsurePendingDecision(application);
        EnsureConcurrencyToken(
            application,
            dto.ConcurrencyToken);

        var before = Snapshot(application);
        var now = GetUtcNow();

        application.Status =
            OrganizationApplicationStatuses
                .UnderReview;
        application.ReviewStartedAtUtc = now;
        application.ReviewedByUserId =
            _currentUserService.UserId;
        application.ConcurrencyToken =
            Guid.NewGuid();

        _repository.SetOriginalConcurrencyToken(
            application,
            dto.ConcurrencyToken);

        await _repository.AddAuditLog(
            CreateAuditLog(
                platformOrganization.Id,
                application,
                AuditActionTypes.Updated,
                "Organization application review " +
                "started.",
                before,
                Snapshot(application)));

        await SaveDecisionChanges();

        return Map(application);
    }

    public async Task<
        OrganizationApplicationApprovalResponseDto>
        Approve(
            Guid applicationId,
            ApproveOrganizationApplicationDto dto)
    {
        var platformOrganization =
            await RequirePlatformAdmin();

        EnsureInvitationDeliveryAvailable();

        var application =
            await GetRequiredApplication(
                applicationId);

        if (application.Status ==
            OrganizationApplicationStatuses
                .Approved)
        {
            return MapExistingApproval(application);
        }

        EnsurePendingDecision(application);
        EnsureAssignedReviewer(application);
        EnsureConcurrencyToken(
            application,
            dto.ConcurrencyToken);

        var organizationCode =
            NormalizeCode(
                dto.OrganizationCode,
                "Organization code");

        var organizationSlug =
            NormalizeSlug(
                dto.OrganizationSlug);

        if (organizationCode ==
                PlatformDefaults.OrganizationCode ||
            organizationSlug ==
                PlatformDefaults.OrganizationSlug)
        {
            throw new ConflictException(
                "The requested organization code or " +
                "slug is reserved.");
        }

        if (await _repository
                .OrganizationCodeExists(
                    organizationCode))
        {
            throw new ConflictException(
                $"Organization code {organizationCode} " +
                "is already in use.");
        }

        if (await _repository
                .OrganizationSlugExists(
                    organizationSlug))
        {
            throw new ConflictException(
                $"Organization slug {organizationSlug} " +
                "is already in use.");
        }

        if (await _repository.UserHasRole(
                application.AdminEmail,
                Roles.PlatformAdmin))
        {
            throw new ConflictException(
                "A PlatformAdmin account cannot be " +
                "onboarded as a customer organization " +
                "administrator.");
        }

        var adminRole =
            await _repository.GetRoleByName(
                Roles.Admin)
            ?? throw new InvalidOperationException(
                "The Admin role has not been seeded.");

        var now = GetUtcNow();
        var organizationId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();

        var organization = new Organization
        {
            Id = organizationId,
            Code = organizationCode,
            Name = application.OrganizationName,
            Slug = organizationSlug,
            CountryCode =
                application.CountryCode,
            BaseCurrency =
                application.BaseCurrency,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        var legalEntity = new LegalEntity
        {
            Id = legalEntityId,
            OrganizationId = organizationId,
            Organization = organization,
            Code = NormalizeCode(
                dto.LegalEntityCode,
                "Legal entity code"),
            Name = NormalizeRequired(
                dto.LegalEntityName,
                "Legal entity name",
                200),
            RegistrationNumber =
                application.RegistrationNumber,
            TaxIdentificationNumber =
                application
                    .TaxIdentificationNumber,
            CountryCode =
                application.CountryCode,
            BaseCurrency =
                application.BaseCurrency,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        var businessUnit = new BusinessUnit
        {
            Id = businessUnitId,
            OrganizationId = organizationId,
            Organization = organization,
            LegalEntityId = legalEntityId,
            LegalEntity = legalEntity,
            Code = NormalizeCode(
                dto.BusinessUnitCode,
                "Business unit code"),
            Name = NormalizeRequired(
                dto.BusinessUnitName,
                "Business unit name",
                200),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        };

        var approvalPolicies =
            CreateApprovalPolicies(
                organizationId,
                application.BaseCurrency,
                dto,
                now);

        var rawToken =
            UserInvitationTokenHelper.Generate();

        var invitation =
            new UserInvitation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Organization = organization,
                Email = application.AdminEmail,
                FirstName =
                    application.AdminFirstName,
                LastName =
                    application.AdminLastName,
                RoleId = adminRole.Id,
                Role = adminRole,
                TokenHash =
                    UserInvitationTokenHelper.Hash(
                        rawToken),
                ExpiresAtUtc =
                    now.AddHours(
                        _invitationOptions
                            .ExpiryHours),
                InvitedByUserId =
                    _currentUserService.UserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            };

        var before = Snapshot(application);

        application.Status =
            OrganizationApplicationStatuses
                .Approved;
        application.ReviewedByUserId =
            _currentUserService.UserId;
        application.ReviewStartedAtUtc ??= now;
        application.DecisionAtUtc = now;
        application.DecisionNotes =
            NormalizeOptional(
                dto.DecisionNotes,
                2000);
        application.ProvisionedOrganizationId =
            organizationId;
        application.ProvisionedOrganization =
            organization;
        application.ProvisionedLegalEntityId =
            legalEntityId;
        application.ProvisionedLegalEntity =
            legalEntity;
        application.ProvisionedBusinessUnitId =
            businessUnitId;
        application.ProvisionedBusinessUnit =
            businessUnit;
        application.AdminInvitationId =
            invitation.Id;
        application.AdminInvitation =
            invitation;
        application.ConcurrencyToken =
            Guid.NewGuid();

        _repository.SetOriginalConcurrencyToken(
            application,
            dto.ConcurrencyToken);

        await _repository.AddProvisioning(
            organization,
            legalEntity,
            businessUnit,
            approvalPolicies,
            invitation);

        await _repository.AddAuditLog(
            CreateAuditLog(
                platformOrganization.Id,
                application,
                AuditActionTypes.Approved,
                "Organization application approved.",
                before,
                Snapshot(application)));

        await _repository.AddAuditLog(
            CreateAuditLog(
                organizationId,
                application,
                AuditActionTypes.Created,
                $"Organization {organizationCode} " +
                "provisioned from an approved " +
                "application.",
                before: null,
                after: new
                {
                    organization.Id,
                    organization.Code,
                    organization.Name,
                    LegalEntityId =
                        legalEntity.Id,
                    BusinessUnitId =
                        businessUnit.Id
                },
                entityType:
                    AuditEntityTypes.Organization,
                entityId: organization.Id,
                entityReference:
                    organization.Code));

        await _repository.AddAuditLog(
            CreateAuditLog(
                organizationId,
                application,
                AuditActionTypes.Created,
                "First organization Admin invitation " +
                "created.",
                before: null,
                after: new
                {
                    invitation.Email,
                    invitation.RoleId,
                    invitation.ExpiresAtUtc
                },
                entityType:
                    AuditEntityTypes.UserInvitation,
                entityId: invitation.Id,
                entityReference:
                    invitation.Email));

        try
        {
            await _repository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The organization application changed " +
                "while it was being approved.");
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The organization could not be " +
                "provisioned. Its code, slug or admin " +
                "invitation may conflict with existing " +
                "data.");
        }

        var delivery =
            await DeliverInvitation(
                invitation,
                rawToken);

        return new
            OrganizationApplicationApprovalResponseDto
            {
                Application = Map(application),
                OrganizationId = organizationId,
                LegalEntityId = legalEntityId,
                BusinessUnitId = businessUnitId,
                AdminInvitationId =
                    invitation.Id,
                InvitationExpiresAtUtc =
                    invitation.ExpiresAtUtc,
                InvitationEmailSent =
                    delivery.EmailSent,
                ManualInvitationAcceptanceUrl =
                    delivery
                        .ManualInvitationAcceptanceUrl
            };
    }

    public async Task<
        OrganizationApplicationResponseDto> Reject(
            Guid applicationId,
            RejectOrganizationApplicationDto dto)
    {
        var platformOrganization =
            await RequirePlatformAdmin();

        var application =
            await GetRequiredApplication(
                applicationId);

        if (application.Status ==
            OrganizationApplicationStatuses
                .Rejected)
        {
            return Map(application);
        }

        EnsurePendingDecision(application);
        EnsureAssignedReviewer(application);
        EnsureConcurrencyToken(
            application,
            dto.ConcurrencyToken);

        var before = Snapshot(application);
        var now = GetUtcNow();

        application.Status =
            OrganizationApplicationStatuses
                .Rejected;
        application.ReviewedByUserId =
            _currentUserService.UserId;
        application.ReviewStartedAtUtc ??= now;
        application.DecisionAtUtc = now;
        application.DecisionNotes =
            NormalizeRequired(
                dto.Reason,
                "Rejection reason",
                2000);
        application.ConcurrencyToken =
            Guid.NewGuid();

        _repository.SetOriginalConcurrencyToken(
            application,
            dto.ConcurrencyToken);

        await _repository.AddAuditLog(
            CreateAuditLog(
                platformOrganization.Id,
                application,
                AuditActionTypes.Rejected,
                "Organization application rejected.",
                before,
                Snapshot(application)));

        await SaveDecisionChanges();

        return Map(application);
    }

    public async Task<
        AdminInvitationDeliveryResponseDto>
        ResendAdminInvitation(Guid applicationId)
    {
        var platformOrganization =
            await RequirePlatformAdmin();

        EnsureInvitationDeliveryAvailable();

        var application =
            await GetRequiredApplication(
                applicationId);

        if (application.Status !=
                OrganizationApplicationStatuses
                    .Approved ||
            application.AdminInvitation is null)
        {
            throw new ConflictException(
                "The application does not have an " +
                "approved Admin invitation.");
        }

        var invitation =
            application.AdminInvitation;

        if (invitation.AcceptedAtUtc.HasValue)
        {
            throw new ConflictException(
                "The Admin invitation has already been " +
                "accepted.");
        }

        if (invitation.RevokedAtUtc.HasValue)
        {
            throw new ConflictException(
                "A revoked Admin invitation cannot be " +
                "resent.");
        }

        var now = GetUtcNow();
        var rawToken =
            UserInvitationTokenHelper.Generate();

        invitation.TokenHash =
            UserInvitationTokenHelper.Hash(
                rawToken);
        invitation.ExpiresAtUtc =
            now.AddHours(
                _invitationOptions.ExpiryHours);
        invitation.UpdatedAtUtc = now;
        invitation.ConcurrencyToken =
            Guid.NewGuid();

        await _repository.AddAuditLog(
            CreateAuditLog(
                platformOrganization.Id,
                application,
                AuditActionTypes.Updated,
                "First organization Admin invitation " +
                "resent.",
                before: null,
                after: new
                {
                    invitation.Id,
                    invitation.Email,
                    invitation.ExpiresAtUtc
                }));

        await _repository.SaveChanges();

        var delivery =
            await DeliverInvitation(
                invitation,
                rawToken);

        return new AdminInvitationDeliveryResponseDto
        {
            InvitationId = invitation.Id,
            ExpiresAtUtc =
                invitation.ExpiresAtUtc,
            EmailSent = delivery.EmailSent,
            ManualInvitationAcceptanceUrl =
                delivery
                    .ManualInvitationAcceptanceUrl
        };
    }

    private async Task<Organization>
        RequirePlatformAdmin()
    {
        if (!string.Equals(
                _currentUserService.Role,
                Roles.PlatformAdmin,
                StringComparison.Ordinal) ||
            !string.Equals(
                _currentUserService.OrganizationCode,
                PlatformDefaults.OrganizationCode,
                StringComparison.Ordinal))
        {
            throw new ForbiddenOperationException(
                "PlatformAdmin access in the reserved " +
                "platform organization is required.");
        }

        var platformOrganization =
            await GetRequiredPlatformOrganization();

        if (_currentUserService.OrganizationId !=
            platformOrganization.Id)
        {
            throw new ForbiddenOperationException(
                "The authenticated PlatformAdmin is not " +
                "in the reserved platform organization.");
        }

        return platformOrganization;
    }

    private async Task<Organization>
        GetRequiredPlatformOrganization()
    {
        var organization =
            await _repository
                .GetPlatformOrganization();

        if (organization is null ||
            !organization.IsActive ||
            organization.Code !=
                PlatformDefaults.OrganizationCode ||
            organization.Slug !=
                PlatformDefaults.OrganizationSlug ||
            organization.Name !=
                PlatformDefaults.OrganizationName)
        {
            throw new InvalidOperationException(
                "The reserved platform organization has " +
                "not been initialized correctly.");
        }

        return organization;
    }

    private async Task<OrganizationApplication>
        GetRequiredApplication(Guid applicationId)
    {
        if (applicationId == Guid.Empty)
        {
            throw new RequestValidationException(
                "Organization application ID is required.");
        }

        return await _repository.GetById(
                   applicationId)
               ?? throw new ResourceNotFoundException(
                   "Organization application was not " +
                   "found.");
    }

    private void EnsureAssignedReviewer(
        OrganizationApplication application)
    {
        if (application.Status ==
                OrganizationApplicationStatuses
                    .UnderReview &&
            application.ReviewedByUserId.HasValue &&
            application.ReviewedByUserId.Value !=
                _currentUserService.UserId)
        {
            throw new ConflictException(
                "The organization application is being " +
                "reviewed by another PlatformAdmin.");
        }
    }

    private static void EnsurePendingDecision(
        OrganizationApplication application)
    {
        if (application.Status ==
            OrganizationApplicationStatuses.Approved)
        {
            throw new ConflictException(
                "The organization application has " +
                "already been approved.");
        }

        if (application.Status ==
            OrganizationApplicationStatuses.Rejected)
        {
            throw new ConflictException(
                "The organization application has " +
                "already been rejected.");
        }
    }

    private static void EnsureConcurrencyToken(
        OrganizationApplication application,
        Guid expectedToken)
    {
        if (expectedToken == Guid.Empty ||
            application.ConcurrencyToken !=
                expectedToken)
        {
            throw new ConflictException(
                "The organization application changed " +
                "in another request. Reload it and try " +
                "again.");
        }
    }

    private void EnsureInvitationDeliveryAvailable()
    {
        if (!_emailSender.IsConfigured &&
            !_onboardingOptions
                .ReturnManualInvitationUrlWhenEmailDisabled)
        {
            throw new BusinessRuleException(
                "Email delivery must be configured before " +
                "approving an organization application.");
        }
    }

    private async Task<
        AdminInvitationDeliveryResponseDto>
        DeliverInvitation(
            UserInvitation invitation,
            string rawToken)
    {
        var acceptanceUrl =
            UserInvitationTokenHelper
                .BuildAcceptanceUrl(
                    _invitationOptions.AcceptanceUrl,
                    rawToken);

        if (!_emailSender.IsConfigured)
        {
            return new
                AdminInvitationDeliveryResponseDto
                {
                    InvitationId = invitation.Id,
                    ExpiresAtUtc =
                        invitation.ExpiresAtUtc,
                    EmailSent = false,
                    ManualInvitationAcceptanceUrl =
                        _onboardingOptions
                            .ReturnManualInvitationUrlWhenEmailDisabled
                            ? acceptanceUrl
                            : null
                };
        }

        try
        {
            await _emailSender.SendUserInvitation(
                invitation.Email,
                $"{invitation.FirstName} " +
                invitation.LastName,
                invitation.Organization.Name,
                acceptanceUrl,
                invitation.ExpiresAtUtc);

            return new
                AdminInvitationDeliveryResponseDto
                {
                    InvitationId = invitation.Id,
                    ExpiresAtUtc =
                        invitation.ExpiresAtUtc,
                    EmailSent = true
                };
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Organization {OrganizationId} was " +
                "provisioned but Admin invitation " +
                "{InvitationId} could not be emailed.",
                invitation.OrganizationId,
                invitation.Id);

            return new
                AdminInvitationDeliveryResponseDto
                {
                    InvitationId = invitation.Id,
                    ExpiresAtUtc =
                        invitation.ExpiresAtUtc,
                    EmailSent = false
                };
        }
    }

    private async Task SaveDecisionChanges()
    {
        try
        {
            await _repository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The organization application changed " +
                "in another request.");
        }
    }

    private IReadOnlyCollection<ApprovalPolicy>
        CreateApprovalPolicies(
            Guid organizationId,
            string currency,
            ApproveOrganizationApplicationDto dto,
            DateTime now)
    {
        return DefaultApprovalOperationTypes
            .Select(operationType =>
                new ApprovalPolicy
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        organizationId,
                    OperationType =
                        operationType,
                    Currency = currency,
                    ThresholdAmount =
                        dto
                            .ApprovalThresholdAmount,
                    RequiredApprovalCount =
                        dto.RequiredApprovalCount,
                    PendingRequestExpiryHours =
                        dto
                            .PendingRequestExpiryHours,
                    IsActive = true,
                    UpdatedByUserId =
                        _currentUserService.UserId,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    ConcurrencyToken =
                        Guid.NewGuid()
                })
            .ToList();
    }

    private AuditLog CreateAuditLog(
        Guid organizationId,
        OrganizationApplication application,
        string action,
        string summary,
        object? before,
        object? after,
        bool includeActor = true,
        string? entityType = null,
        Guid? entityId = null,
        string? entityReference = null)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ActorUserId =
                includeActor
                    ? TryGetActorUserId()
                    : null,
            ActorEmail =
                includeActor
                    ? _currentUserService.Email
                    : null,
            ActorRole =
                includeActor
                    ? _currentUserService.Role
                    : null,
            Action = action,
            EntityType =
                entityType ??
                AuditEntityTypes
                    .OrganizationApplication,
            EntityId =
                entityId ?? application.Id,
            EntityReference =
                entityReference ??
                application.OrganizationName,
            Summary = summary,
            BeforeValuesJson =
                Serialize(before),
            AfterValuesJson =
                Serialize(after),
            MetadataJson =
                Serialize(
                    new
                    {
                        Module =
                            "Organization Onboarding",
                        application.SubmissionKey
                    }),
            IpAddress =
                _clientRequestContext.IpAddress,
            UserAgent =
                _clientRequestContext.UserAgent,
            OccurredAtUtc = GetUtcNow()
        };
    }

    private Guid? TryGetActorUserId()
    {
        try
        {
            return _currentUserService.UserId ==
                   Guid.Empty
                ? null
                : _currentUserService.UserId;
        }
        catch
        {
            return null;
        }
    }

    private static string? Serialize(object? value)
    {
        return value is null
            ? null
            : JsonSerializer.Serialize(
                value,
                JsonOptions);
    }

    private static object Snapshot(
        OrganizationApplication application)
    {
        return new
        {
            application.Id,
            application.OrganizationName,
            application.CountryCode,
            application.BaseCurrency,
            application.AdminEmail,
            application.Status,
            application.ReviewStartedAtUtc,
            application.DecisionAtUtc,
            application.ReviewedByUserId,
            application.ProvisionedOrganizationId,
            application.ProvisionedLegalEntityId,
            application.ProvisionedBusinessUnitId,
            application.AdminInvitationId
        };
    }

    private static OrganizationApplicationResponseDto
        Map(OrganizationApplication application)
    {
        return new OrganizationApplicationResponseDto
        {
            Id = application.Id,
            SubmissionKey =
                application.SubmissionKey,
            OrganizationName =
                application.OrganizationName,
            RegistrationNumber =
                application.RegistrationNumber,
            TaxIdentificationNumber =
                application
                    .TaxIdentificationNumber,
            CountryCode =
                application.CountryCode,
            BaseCurrency =
                application.BaseCurrency,
            AdminFirstName =
                application.AdminFirstName,
            AdminLastName =
                application.AdminLastName,
            AdminEmail =
                application.AdminEmail,
            ContactPhoneNumber =
                application.ContactPhoneNumber,
            ApplicationNotes =
                application.ApplicationNotes,
            Status = application.Status,
            DecisionNotes =
                application.DecisionNotes,
            SubmittedAtUtc =
                application.SubmittedAtUtc,
            ReviewStartedAtUtc =
                application.ReviewStartedAtUtc,
            DecisionAtUtc =
                application.DecisionAtUtc,
            ReviewedByUserId =
                application.ReviewedByUserId,
            ProvisionedOrganizationId =
                application
                    .ProvisionedOrganizationId,
            ProvisionedLegalEntityId =
                application
                    .ProvisionedLegalEntityId,
            ProvisionedBusinessUnitId =
                application
                    .ProvisionedBusinessUnitId,
            AdminInvitationId =
                application.AdminInvitationId,
            ConcurrencyToken =
                application.ConcurrencyToken
        };
    }

    private static
        OrganizationApplicationApprovalResponseDto
        MapExistingApproval(
            OrganizationApplication application)
    {
        if (!application
                .ProvisionedOrganizationId
                .HasValue ||
            !application
                .ProvisionedLegalEntityId
                .HasValue ||
            !application
                .ProvisionedBusinessUnitId
                .HasValue ||
            application.AdminInvitation is null)
        {
            throw new InvalidOperationException(
                "The approved organization application " +
                "has incomplete provisioning data.");
        }

        return new
            OrganizationApplicationApprovalResponseDto
            {
                Application = Map(application),
                OrganizationId =
                    application
                        .ProvisionedOrganizationId
                        .Value,
                LegalEntityId =
                    application
                        .ProvisionedLegalEntityId
                        .Value,
                BusinessUnitId =
                    application
                        .ProvisionedBusinessUnitId
                        .Value,
                AdminInvitationId =
                    application
                        .AdminInvitation.Id,
                InvitationExpiresAtUtc =
                    application
                        .AdminInvitation
                        .ExpiresAtUtc,
                InvitationEmailSent = false
            };
    }

    private static NormalizedSubmission
        NormalizeSubmission(
            SubmitOrganizationApplicationDto dto)
    {
        var organizationName =
            NormalizeRequired(
                dto.OrganizationName,
                "Organization name",
                200);

        return new NormalizedSubmission(
            organizationName,
            organizationName.ToUpperInvariant(),
            NormalizeOptional(
                dto.RegistrationNumber,
                100),
            NormalizeOptional(
                dto.TaxIdentificationNumber,
                100),
            NormalizeCountryCode(dto.CountryCode),
            NormalizeCurrency(dto.BaseCurrency),
            NormalizeRequired(
                dto.AdminFirstName,
                "Admin first name",
                100),
            NormalizeRequired(
                dto.AdminLastName,
                "Admin last name",
                100),
            dto.AdminEmail
                .Trim()
                .ToLowerInvariant(),
            NormalizeOptional(
                dto.ContactPhoneNumber,
                30),
            NormalizeOptional(
                dto.ApplicationNotes,
                2000));
    }

    private static bool Matches(
        OrganizationApplication application,
        NormalizedSubmission submission)
    {
        return application.OrganizationName ==
                   submission.OrganizationName &&
               application.NormalizedOrganizationName ==
                   submission
                       .NormalizedOrganizationName &&
               application.RegistrationNumber ==
                   submission.RegistrationNumber &&
               application.TaxIdentificationNumber ==
                   submission
                       .TaxIdentificationNumber &&
               application.CountryCode ==
                   submission.CountryCode &&
               application.BaseCurrency ==
                   submission.BaseCurrency &&
               application.AdminFirstName ==
                   submission.AdminFirstName &&
               application.AdminLastName ==
                   submission.AdminLastName &&
               application.AdminEmail ==
                   submission.AdminEmail &&
               application.ContactPhoneNumber ==
                   submission.ContactPhoneNumber &&
               application.ApplicationNotes ==
                   submission.ApplicationNotes;
    }

    private static string NormalizeStatus(
        string value)
    {
        var status =
            OrganizationApplicationStatuses.All
                .FirstOrDefault(item =>
                    string.Equals(
                        item,
                        value.Trim(),
                        StringComparison
                            .OrdinalIgnoreCase));

        return status ??
               throw new RequestValidationException(
                   "Organization application status is " +
                   "invalid.");
    }

    private static string NormalizeCode(
        string value,
        string fieldName)
    {
        var normalized =
            NormalizeRequired(
                    value,
                    fieldName,
                    50)
                .ToUpperInvariant();

        if (!System.Text.RegularExpressions.Regex
                .IsMatch(
                    normalized,
                    "^[A-Z0-9][A-Z0-9-]{0,49}$"))
        {
            throw new RequestValidationException(
                $"{fieldName} can contain only letters, " +
                "numbers and hyphens.");
        }

        return normalized;
    }

    private static string NormalizeSlug(
        string value)
    {
        var normalized =
            NormalizeRequired(
                    value,
                    "Organization slug",
                    100)
                .ToLowerInvariant();

        if (!System.Text.RegularExpressions.Regex
                .IsMatch(
                    normalized,
                    "^[a-z0-9][a-z0-9-]{0,99}$"))
        {
            throw new RequestValidationException(
                "Organization slug can contain only " +
                "lower-case letters, numbers and " +
                "hyphens.");
        }

        return normalized;
    }

    private static string NormalizeCountryCode(
        string value)
    {
        var normalized =
            value.Trim().ToUpperInvariant();

        if (!System.Text.RegularExpressions.Regex
                .IsMatch(
                    normalized,
                    "^[A-Z]{2}$"))
        {
            throw new RequestValidationException(
                "Country code must contain exactly two " +
                "letters.");
        }

        return normalized;
    }

    private static string NormalizeCurrency(
        string value)
    {
        var normalized =
            value.Trim().ToUpperInvariant();

        if (!System.Text.RegularExpressions.Regex
                .IsMatch(
                    normalized,
                    "^[A-Z]{3}$"))
        {
            throw new RequestValidationException(
                "Base currency must contain exactly " +
                "three letters.");
        }

        return normalized;
    }

    private static string NormalizeRequired(
        string? value,
        string fieldName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RequestValidationException(
                $"{fieldName} is required.");
        }

        var normalized = value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new RequestValidationException(
                $"{fieldName} cannot exceed " +
                $"{maximumLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new RequestValidationException(
                "The supplied value exceeds its maximum " +
                $"length of {maximumLength} characters.");
        }

        return normalized;
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider
            .GetUtcNow()
            .UtcDateTime;
    }

    private sealed record NormalizedSubmission(
        string OrganizationName,
        string NormalizedOrganizationName,
        string? RegistrationNumber,
        string? TaxIdentificationNumber,
        string CountryCode,
        string BaseCurrency,
        string AdminFirstName,
        string AdminLastName,
        string AdminEmail,
        string? ContactPhoneNumber,
        string? ApplicationNotes);
}
