using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.ApprovalPolicies;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class ApprovalPolicyService
    : IApprovalPolicyService
{
    private readonly IApprovalPolicyRepository
        _policyRepository;

    private readonly ICurrentUserService
        _currentUserService;

    public ApprovalPolicyService(
        IApprovalPolicyRepository policyRepository,
        ICurrentUserService currentUserService)
    {
        _policyRepository =
            policyRepository;

        _currentUserService =
            currentUserService;
    }

    public async Task<decimal> GetThreshold(
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

        return policy.ThresholdAmount;
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

        return Map(policy);
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

            IsActive =
                policy.IsActive,

            CreatedAtUtc =
                policy.CreatedAtUtc,

            UpdatedAtUtc =
                policy.UpdatedAtUtc

        };
    }
}