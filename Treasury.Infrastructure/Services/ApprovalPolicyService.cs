using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.ApprovalPolicies;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class ApprovalPolicyService
    : IApprovalPolicyService
{
    private readonly IApprovalPolicyRepository
        _policyRepository;

    private readonly ICurrentUserService _currentUserService;
    
    private readonly IAuditLogService _auditLogService;

    public ApprovalPolicyService(
        IApprovalPolicyRepository policyRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _policyRepository =
            policyRepository;

        _currentUserService =
            currentUserService;

        _auditLogService = 
            auditLogService;
    }

    public async Task<decimal> GetThreshold(
        string operationType,
        string currency)
    {
        var requirements =
            await GetRequirements(
                operationType,
                currency);

        return requirements.ThresholdAmount;
    }

    public async Task<ApprovalRequirementsDto>
        GetRequirements(
            string operationType,
            string currency)
    {
        var normalizedOperation =
            NormalizeOperationType(
                operationType);

        var normalizedCurrency =
            NormalizeCurrency(currency);

        var policy =
            await _policyRepository.GetActive(
                normalizedOperation,
                normalizedCurrency);

        if (policy is null)
        {
            throw new BusinessRuleException(
                $"No active approval policy exists " +
                $"for {normalizedOperation} in " +
                $"{normalizedCurrency}.");
        }

        return new ApprovalRequirementsDto
        {
            ThresholdAmount =
                policy.ThresholdAmount,

            RequiredApprovalCount =
                policy.RequiredApprovalCount,

            PendingRequestExpiryHours =
                policy.PendingRequestExpiryHours
        };
    }

    public async Task<List<ApprovalPolicyDto>>
        GetAll()
    {
        var policies =
            await _policyRepository.GetAll();

        return policies
            .Select(Map)
            .ToList();
    }

    public async Task<ApprovalPolicyDto>
        SavePolicy(
            UpdateApprovalPolicyDto dto)
    {
        if (dto.ThresholdAmount < 0)
        {
            throw new RequestValidationException(
                "Approval threshold cannot " +
                "be negative.");
        }

        if (dto.RequiredApprovalCount < 1 ||
            dto.RequiredApprovalCount > 5)
        {
            throw new RequestValidationException(
                "Required approval count must be " +
                "between 1 and 5.");
        }

        if (dto.PendingRequestExpiryHours < 1 ||
            dto.PendingRequestExpiryHours > 168)
        {
            throw new RequestValidationException(
                "Pending request expiry must be " +
                "between 1 and 168 hours.");
        }

        var operationType =
            NormalizeOperationType(
                dto.OperationType);

        var currency =
            NormalizeCurrency(
                dto.Currency);

        var policy =
            await _policyRepository.GetByKey(
                operationType,
                currency);

        var isNewPolicy =
            policy is null;

        var beforeValues =
            policy is null
                ? null
                : SnapshotPolicy(policy);

        if (policy is null)
        {
            policy = new ApprovalPolicy
            {
                Id = Guid.NewGuid(),

                OperationType =
                    operationType,

                Currency =
                    currency,

                ThresholdAmount =
                    dto.ThresholdAmount,

                IsActive =
                    dto.IsActive,

                UpdatedByUserId =
                    _currentUserService.UserId,

                RequiredApprovalCount =
                    dto.RequiredApprovalCount,
                
                PendingRequestExpiryHours =
                    dto.PendingRequestExpiryHours,

                CreatedAtUtc =
                    DateTime.UtcNow,

                UpdatedAtUtc =
                    DateTime.UtcNow,

                ConcurrencyToken =
                    Guid.NewGuid()
            };

            await _policyRepository
                .Add(policy);
        }
        else
        {
            policy.ThresholdAmount =
                dto.ThresholdAmount;

            policy.IsActive =
                dto.IsActive;

            policy.UpdatedByUserId =
                _currentUserService.UserId;

            policy.UpdatedAtUtc =
                DateTime.UtcNow;
    
            policy.RequiredApprovalCount =
                dto.RequiredApprovalCount;
    
            policy.PendingRequestExpiryHours =
                dto.PendingRequestExpiryHours;

            policy.ConcurrencyToken =
                Guid.NewGuid();
        }

        try
        {
            await _policyRepository
                .SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The approval policy was changed " +
                "by another administrator.");
        }

        var response =
            Map(policy);

        await RecordApprovalPolicyAudit(
            isNewPolicy,
            beforeValues,
            response);

        return response;
    }

    private async Task RecordApprovalPolicyAudit(
        bool isNewPolicy,
        object? beforeValues,
        ApprovalPolicyDto policy)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    isNewPolicy
                        ? AuditActionTypes.Created
                        : AuditActionTypes.Updated,

                EntityType =
                    AuditEntityTypes.ApprovalPolicy,

                EntityId =
                    policy.Id,

                EntityReference =
                    $"{policy.OperationType}-{policy.Currency}",

                Summary =
                    isNewPolicy
                        ? $"Approval policy for {policy.OperationType} " +
                        $"{policy.Currency} was created."
                        : $"Approval policy for {policy.OperationType} " +
                        $"{policy.Currency} was updated.",

                BeforeValues =
                    beforeValues,

                AfterValues =
                    SnapshotPolicy(policy),

                Metadata =
                    new
                    {
                        Module = "Approval Policies",
                        policy.OperationType,
                        policy.Currency
                    }
            });
    }

    private static object SnapshotPolicy(
        ApprovalPolicy policy)
    {
        return new
        {
            policy.Id,
            policy.OperationType,
            policy.Currency,
            policy.ThresholdAmount,
            policy.RequiredApprovalCount,
            policy.PendingRequestExpiryHours,
            policy.IsActive,
            policy.UpdatedByUserId,
            policy.CreatedAtUtc,
            policy.UpdatedAtUtc
        };
    }

    private static object SnapshotPolicy(
        ApprovalPolicyDto policy)
    {
        return new
        {
            policy.Id,
            policy.OperationType,
            policy.Currency,
            policy.ThresholdAmount,
            policy.RequiredApprovalCount,
            policy.PendingRequestExpiryHours,
            policy.IsActive,
            policy.CreatedAtUtc,
            policy.UpdatedAtUtc
        };
    }

    private static string
        NormalizeOperationType(
            string operationType)
    {
        if (string.Equals(
            operationType,
            ApprovalOperationTypes
                .InternalTransfer,
            StringComparison.OrdinalIgnoreCase))
        {
            return ApprovalOperationTypes
                .InternalTransfer;
        }

        if (string.Equals(
            operationType,
            ApprovalOperationTypes
                .CashPayment,
            StringComparison.OrdinalIgnoreCase))
        {
            return ApprovalOperationTypes
                .CashPayment;
        }

        if (string.Equals(
            operationType,
            ApprovalOperationTypes
                .TransactionReversal,
            StringComparison.OrdinalIgnoreCase))
        {
            return ApprovalOperationTypes
                .TransactionReversal;
        }

        if (string.Equals(
                operationType,
                ApprovalOperationTypes
                    .InvestmentPlacement,
                StringComparison.OrdinalIgnoreCase))
        {
            return ApprovalOperationTypes
                .InvestmentPlacement;
        }

        throw new RequestValidationException(
            "Unsupported approval operation type.");
    }

    private static string NormalizeCurrency(
        string currency)
    {
        var normalized =
            currency?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(
                normalized) ||
            normalized.Length != 3 ||
            !normalized.All(char.IsLetter))
        {
            throw new RequestValidationException(
                "Currency must be a valid " +
                "three-letter code.");
        }

        return normalized;
    }

    private static ApprovalPolicyDto Map(
        ApprovalPolicy policy)
    {
        return new ApprovalPolicyDto
        {
            Id = policy.Id,

            OperationType =
                policy.OperationType,

            Currency =
                policy.Currency,

            ThresholdAmount =
                policy.ThresholdAmount,

            RequiredApprovalCount =
                policy.RequiredApprovalCount,
            
            PendingRequestExpiryHours =
                policy.PendingRequestExpiryHours,

            IsActive =
                policy.IsActive,

            CreatedAtUtc =
                policy.CreatedAtUtc,

            UpdatedAtUtc =
                policy.UpdatedAtUtc

        };
    }
}