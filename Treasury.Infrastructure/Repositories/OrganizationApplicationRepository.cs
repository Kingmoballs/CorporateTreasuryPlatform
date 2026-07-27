using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.OrganizationOnboarding;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Repositories;

public class OrganizationApplicationRepository
    : IOrganizationApplicationRepository
{
    private readonly TreasuryDbContext _context;

    public OrganizationApplicationRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public Task<OrganizationApplication?> GetById(
        Guid applicationId)
    {
        return BaseQuery()
            .FirstOrDefaultAsync(application =>
                application.Id == applicationId);
    }

    public Task<OrganizationApplication?>
        GetBySubmissionKey(Guid submissionKey)
    {
        return BaseQuery()
            .FirstOrDefaultAsync(application =>
                application.SubmissionKey ==
                    submissionKey);
    }

    public async Task<(
        IReadOnlyList<OrganizationApplication> Items,
        int TotalCount)> Search(
            OrganizationApplicationQueryDto query)
    {
        var applications =
            _context.OrganizationApplications
                .AsNoTracking()
                .AsQueryable();

        if (!string.IsNullOrWhiteSpace(
                query.Status))
        {
            applications =
                applications.Where(application =>
                    application.Status ==
                        query.Status);
        }

        if (!string.IsNullOrWhiteSpace(
                query.Search))
        {
            var search = query.Search.Trim();

            applications =
                applications.Where(application =>
                    EF.Functions.ILike(
                        application.OrganizationName,
                        $"%{search}%") ||
                    EF.Functions.ILike(
                        application.AdminEmail,
                        $"%{search}%") ||
                    (application.RegistrationNumber !=
                         null &&
                     EF.Functions.ILike(
                         application
                             .RegistrationNumber,
                         $"%{search}%")));
        }

        var totalCount =
            await applications.CountAsync();

        var items =
            await applications
                .OrderByDescending(application =>
                    application.SubmittedAtUtc)
                .Skip(
                    (query.Page - 1) *
                    query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

        return (items, totalCount);
    }

    public Task<Organization?>
        GetPlatformOrganization()
    {
        return _context.Organizations
            .FirstOrDefaultAsync(organization =>
                organization.Code ==
                    PlatformDefaults
                        .OrganizationCode);
    }

    public Task<Role?> GetRoleByName(string name)
    {
        return _context.Roles
            .FirstOrDefaultAsync(role =>
                role.Name == name);
    }

    public Task<bool> OrganizationCodeExists(
        string code)
    {
        return _context.Organizations
            .AsNoTracking()
            .AnyAsync(organization =>
                organization.Code == code);
    }

    public Task<bool> OrganizationSlugExists(
        string slug)
    {
        return _context.Organizations
            .AsNoTracking()
            .AnyAsync(organization =>
                organization.Slug == slug);
    }

    public Task<bool> UserHasRole(
        string normalizedEmail,
        string roleName)
    {
        return _context.Users
            .AsNoTracking()
            .AnyAsync(user =>
                user.Email == normalizedEmail &&
                user.OrganizationMemberships
                    .Any(membership =>
                        membership.Role.Name ==
                            roleName));
    }

    public Task<bool> HasOpenApplication(
        string normalizedOrganizationName,
        string normalizedAdminEmail)
    {
        return _context.OrganizationApplications
            .AsNoTracking()
            .AnyAsync(application =>
                application
                    .NormalizedOrganizationName ==
                        normalizedOrganizationName &&
                application.AdminEmail ==
                    normalizedAdminEmail &&
                (application.Status ==
                    OrganizationApplicationStatuses
                        .Submitted ||
                 application.Status ==
                    OrganizationApplicationStatuses
                        .UnderReview));
    }

    public async Task AddApplication(
        OrganizationApplication application)
    {
        await _context.OrganizationApplications
            .AddAsync(application);
    }

    public async Task AddProvisioning(
        Organization organization,
        LegalEntity legalEntity,
        BusinessUnit businessUnit,
        IReadOnlyCollection<ApprovalPolicy>
            approvalPolicies,
        UserInvitation invitation)
    {
        await _context.Organizations.AddAsync(
            organization);

        await _context.LegalEntities.AddAsync(
            legalEntity);

        await _context.BusinessUnits.AddAsync(
            businessUnit);

        await _context.ApprovalPolicies
            .AddRangeAsync(approvalPolicies);

        await _context.UserInvitations.AddAsync(
            invitation);
    }

    public async Task AddAuditLog(
        AuditLog auditLog)
    {
        await _context.AuditLogs.AddAsync(
            auditLog);
    }

    public void SetOriginalConcurrencyToken(
        OrganizationApplication application,
        Guid concurrencyToken)
    {
        _context.Entry(application)
            .Property(item =>
                item.ConcurrencyToken)
            .OriginalValue =
                concurrencyToken;
    }

    public Task SaveChanges()
    {
        return _context.SaveChangesAsync();
    }

    private IQueryable<OrganizationApplication>
        BaseQuery()
    {
        return _context.OrganizationApplications
            .Include(application =>
                application.ReviewedByUser)
            .Include(application =>
                application
                    .ProvisionedOrganization)
            .Include(application =>
                application.AdminInvitation)
                .ThenInclude(invitation =>
                    invitation!.Organization)
            .Include(application =>
                application.AdminInvitation)
                .ThenInclude(invitation =>
                    invitation!.Role);
    }
}
