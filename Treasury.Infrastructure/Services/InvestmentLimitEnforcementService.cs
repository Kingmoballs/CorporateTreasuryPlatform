using System.Text.RegularExpressions;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class InvestmentLimitEnforcementService
    : IInvestmentLimitEnforcementService
{
    private readonly ICounterpartyRepository
        _counterpartyRepository;

    private readonly IInvestmentLimitRepository
        _investmentLimitRepository;

    private readonly IInvestmentPlacementRepository
        _investmentPlacementRepository;

    public InvestmentLimitEnforcementService(
        ICounterpartyRepository counterpartyRepository,
        IInvestmentLimitRepository
            investmentLimitRepository,
        IInvestmentPlacementRepository
            investmentPlacementRepository)
    {
        _counterpartyRepository =
            counterpartyRepository;

        _investmentLimitRepository =
            investmentLimitRepository;

        _investmentPlacementRepository =
            investmentPlacementRepository;
    }

    public async Task EnsureWithinLimits(
        Guid counterpartyId,
        string currency,
        string investmentType,
        decimal proposedPrincipalAmount,
        Guid? excludedPlacementId)
    {
        if (counterpartyId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "The investment must be assigned to " +
                "a counterparty before activation.");
        }

        var normalizedCurrency =
            NormalizeCurrency(currency);

        var normalizedInvestmentType =
            NormalizeInvestmentType(
                investmentType);

        var proposedPrincipal =
            RoundMoney(
                proposedPrincipalAmount);

        if (proposedPrincipal <= 0)
        {
            throw new BusinessRuleException(
                "Proposed investment principal must " +
                "be greater than zero.");
        }

        /*
         * The counterparty lock is always acquired before
         * the limit locks. Consistent lock ordering reduces
         * the risk of database deadlocks.
         */
        var counterparty =
            await _counterpartyRepository
                .GetByIdForUpdate(
                    counterpartyId);

        if (counterparty is null)
        {
            throw new ResourceNotFoundException(
                "Investment counterparty was not found.");
        }

        if (!counterparty.IsActive)
        {
            throw new ConflictException(
                $"Counterparty {counterparty.Code} is " +
                "inactive and cannot receive new exposure.");
        }

        var asOfUtc =
            DateTime.UtcNow;

        var applicableLimits =
            await _investmentLimitRepository
                .GetApplicableActiveLimitsForUpdate(
                    counterparty.Id,
                    normalizedCurrency,
                    normalizedInvestmentType,
                    asOfUtc);

        var overallLimit =
            GetRequiredSingleLimit(
                applicableLimits,
                InvestmentLimitScopes
                    .AllInvestmentTypes,
                counterparty.Code,
                normalizedCurrency);

        var productLimit =
            GetRequiredSingleLimit(
                applicableLimits,
                normalizedInvestmentType,
                counterparty.Code,
                normalizedCurrency);

        var committedPlacements =
            await _investmentPlacementRepository
                .GetForLimitUtilization(
                    counterparty.Id,
                    normalizedCurrency);

        if (excludedPlacementId.HasValue)
        {
            committedPlacements =
                committedPlacements
                    .Where(placement =>
                        placement.Id !=
                            excludedPlacementId.Value)
                    .ToList();
        }

        var overallCurrentExposure =
            RoundMoney(
                committedPlacements.Sum(
                    placement =>
                        placement.PrincipalAmount));

        var productCurrentExposure =
            RoundMoney(
                committedPlacements
                    .Where(placement =>
                        string.Equals(
                            placement.InvestmentType,
                            normalizedInvestmentType,
                            StringComparison
                                .OrdinalIgnoreCase))
                    .Sum(placement =>
                        placement.PrincipalAmount));

        EnsureProjectedExposureAllowed(
            overallLimit,
            overallCurrentExposure,
            proposedPrincipal,
            counterparty.Code);

        EnsureProjectedExposureAllowed(
            productLimit,
            productCurrentExposure,
            proposedPrincipal,
            counterparty.Code);
    }

    private static InvestmentLimit
        GetRequiredSingleLimit(
            IReadOnlyList<InvestmentLimit> limits,
            string investmentType,
            string counterpartyCode,
            string currency)
    {
        var matchingLimits =
            limits
                .Where(limit =>
                    string.Equals(
                        limit.InvestmentType,
                        investmentType,
                        StringComparison
                            .OrdinalIgnoreCase))
                .ToList();

        if (matchingLimits.Count == 0)
        {
            throw new ConflictException(
                $"No active {investmentType} investment " +
                $"limit is configured for counterparty " +
                $"{counterpartyCode} in {currency}.");
        }

        if (matchingLimits.Count > 1)
        {
            throw new ConflictException(
                $"Multiple active {investmentType} limits " +
                $"are applicable to counterparty " +
                $"{counterpartyCode} in {currency}. " +
                "Correct the overlapping limit periods.");
        }

        return matchingLimits[0];
    }

    private static void
        EnsureProjectedExposureAllowed(
            InvestmentLimit limit,
            decimal currentExposure,
            decimal proposedPrincipal,
            string counterpartyCode)
    {
        var projectedExposure =
            RoundMoney(
                currentExposure +
                proposedPrincipal);

        if (projectedExposure <=
            limit.MaximumExposureAmount)
        {
            return;
        }

        var excessAmount =
            RoundMoney(
                projectedExposure -
                limit.MaximumExposureAmount);

        throw new BusinessRuleException(
            $"Investment limit exceeded for " +
            $"{counterpartyCode}. " +
            $"Scope: {limit.InvestmentType}; " +
            $"Currency: {limit.Currency}; " +
            $"Current exposure: " +
            $"{currentExposure:N2}; " +
            $"Proposed principal: " +
            $"{proposedPrincipal:N2}; " +
            $"Limit: " +
            $"{limit.MaximumExposureAmount:N2}; " +
            $"Excess: {excessAmount:N2}.");
    }

    private static string NormalizeCurrency(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                "Investment currency is required.");
        }

        var currency =
            value.Trim().ToUpperInvariant();

        if (!Regex.IsMatch(
                currency,
                "^[A-Z]{3}$"))
        {
            throw new BusinessRuleException(
                "Investment currency must contain " +
                "exactly three letters.");
        }

        return currency;
    }

    private static string NormalizeInvestmentType(
        string? value)
    {
        if (string.Equals(
                value?.Trim(),
                InvestmentPlacementTypes
                    .FixedDeposit,
                StringComparison.OrdinalIgnoreCase))
        {
            return InvestmentPlacementTypes
                .FixedDeposit;
        }

        throw new BusinessRuleException(
            "Investment type must be FixedDeposit.");
    }

    private static decimal RoundMoney(
        decimal value)
    {
        return Math.Round(
            value,
            2,
            MidpointRounding.AwayFromZero);
    }
}