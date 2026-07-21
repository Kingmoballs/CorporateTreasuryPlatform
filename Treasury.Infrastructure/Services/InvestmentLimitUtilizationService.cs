using System.Text.RegularExpressions;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.InvestmentLimits;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class InvestmentLimitUtilizationService
    : IInvestmentLimitUtilizationService
{
    private readonly IInvestmentLimitRepository
        _investmentLimitRepository;

    private readonly IInvestmentPlacementRepository
        _investmentPlacementRepository;

    public InvestmentLimitUtilizationService(
        IInvestmentLimitRepository
            investmentLimitRepository,
        IInvestmentPlacementRepository
            investmentPlacementRepository)
    {
        _investmentLimitRepository =
            investmentLimitRepository;

        _investmentPlacementRepository =
            investmentPlacementRepository;
    }

    public async Task<
        InvestmentLimitUtilizationReportDto>
        GetUtilization(
            InvestmentLimitUtilizationQueryDto query)
    {
        ValidateCounterpartyId(
            query.CounterpartyId);

        var currency =
            string.IsNullOrWhiteSpace(
                query.Currency)
                ? null
                : NormalizeCurrency(
                    query.Currency);

        var effectiveAtUtc =
            query.AsOfUtc.HasValue
                ? NormalizeUtc(
                    query.AsOfUtc.Value)
                : DateTime.UtcNow;

        var limits =
            await _investmentLimitRepository
                .GetApplicableActiveLimits(
                    query.CounterpartyId,
                    currency,
                    effectiveAtUtc);

        var placements =
            await _investmentPlacementRepository
                .GetForLimitUtilization(
                    query.CounterpartyId,
                    currency);

        var items =
            limits
                .Select(limit =>
                    BuildItem(
                        limit,
                        placements))
                .OrderBy(item =>
                    item.CounterpartyName)
                .ThenBy(item =>
                    item.Currency)
                .ThenBy(item =>
                    item.InvestmentType)
                .ToList();

        return new InvestmentLimitUtilizationReportDto
        {
            GeneratedAtUtc =
                DateTime.UtcNow,

            EffectiveAtUtc =
                effectiveAtUtc,

            LimitCount =
                items.Count,

            WarningCount =
                items.Count(item =>
                    item.Status ==
                        InvestmentLimitUtilizationStatuses
                            .Warning),

            BreachedCount =
                items.Count(item =>
                    item.Status ==
                        InvestmentLimitUtilizationStatuses
                            .Breached),

            UnassignedPlacementCount =
                placements.Count(placement =>
                    !placement.CounterpartyId.HasValue),

            Items =
                items
        };
    }

    private static InvestmentLimitUtilizationItemDto
        BuildItem(
            InvestmentLimit limit,
            IReadOnlyList<InvestmentPlacement>
                placements)
    {
        var applicablePlacements =
            placements
                .Where(placement =>
                    placement.CounterpartyId ==
                        limit.CounterpartyId &&
                    placement.Currency ==
                        limit.Currency &&
                    IsWithinInvestmentTypeScope(
                        placement,
                        limit))
                .ToList();

        var exposure =
            RoundMoney(
                applicablePlacements.Sum(
                    placement =>
                        placement.PrincipalAmount));

        var maximum =
            limit.MaximumExposureAmount;

        var warningAmount =
            RoundMoney(
                maximum *
                limit.WarningThresholdPercentage /
                100m);

        var available =
            RoundMoney(
                Math.Max(
                    0m,
                    maximum - exposure));

        var breachAmount =
            RoundMoney(
                Math.Max(
                    0m,
                    exposure - maximum));

        var utilizationPercentage =
            maximum <= 0
                ? 0m
                : Math.Round(
                    exposure * 100m / maximum,
                    2,
                    MidpointRounding.AwayFromZero);

        var status =
            exposure > maximum
                ? InvestmentLimitUtilizationStatuses
                    .Breached
                : exposure >= warningAmount
                    ? InvestmentLimitUtilizationStatuses
                        .Warning
                    : InvestmentLimitUtilizationStatuses
                        .WithinLimit;

        return new InvestmentLimitUtilizationItemDto
        {
            InvestmentLimitId =
                limit.Id,

            CounterpartyId =
                limit.CounterpartyId,

            CounterpartyCode =
                limit.Counterparty?.Code ??
                string.Empty,

            CounterpartyName =
                limit.Counterparty?.Name ??
                string.Empty,

            Currency =
                limit.Currency,

            InvestmentType =
                limit.InvestmentType,

            MaximumExposureAmount =
                maximum,

            WarningThresholdPercentage =
                limit.WarningThresholdPercentage,

            WarningThresholdAmount =
                warningAmount,

            PlacementCount =
                applicablePlacements.Count,

            CurrentExposureAmount =
                exposure,

            AvailableLimitAmount =
                available,

            BreachAmount =
                breachAmount,

            UtilizationPercentage =
                utilizationPercentage,

            Status =
                status,

            EffectiveFromUtc =
                limit.EffectiveFromUtc,

            EffectiveToUtc =
                limit.EffectiveToUtc
        };
    }

    private static bool IsWithinInvestmentTypeScope(
        InvestmentPlacement placement,
        InvestmentLimit limit)
    {
        return limit.InvestmentType ==
                InvestmentLimitScopes
                    .AllInvestmentTypes ||
            string.Equals(
                placement.InvestmentType,
                limit.InvestmentType,
                StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateCounterpartyId(
        Guid? counterpartyId)
    {
        if (counterpartyId.HasValue &&
            counterpartyId.Value == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Counterparty ID is invalid.");
        }
    }

    private static string NormalizeCurrency(
        string value)
    {
        var currency =
            value.Trim().ToUpperInvariant();

        if (!Regex.IsMatch(
                currency,
                "^[A-Z]{3}$"))
        {
            throw new BusinessRuleException(
                "Currency must contain exactly " +
                "three letters.");
        }

        return currency;
    }

    private static DateTime NormalizeUtc(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc =>
                value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc)
        };
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