using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class InvestmentAccrualService
    : IInvestmentAccrualService
{
    private readonly IInvestmentPlacementRepository
        _placementRepository;

    public InvestmentAccrualService(
        IInvestmentPlacementRepository placementRepository)
    {
        _placementRepository =
            placementRepository;
    }

    public async Task<InvestmentAccrualReportDto> GetReport(
        InvestmentAccrualQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalizedQuery =
            NormalizeQuery(query);

        var asOfUtc =
            normalizedQuery.AsOfUtc!.Value;

        var placements =
            await _placementRepository.GetForReporting(
                new InvestmentPortfolioQueryDto
                {
                    Currency =
                        normalizedQuery.Currency,

                    InstitutionName =
                        normalizedQuery.InstitutionName,

                    IncludeRedeemed =
                        normalizedQuery.IncludeRedeemed
                });

        var items =
            placements
                .Select(placement =>
                    MapAccrualItem(
                        placement,
                        asOfUtc))
                .OrderBy(item =>
                    item.Currency)
                .ThenBy(item =>
                    item.InstitutionName)
                .ThenBy(item =>
                    item.MaturityDateUtc)
                .ToList();

        var currencySummaries =
            items
                .GroupBy(item =>
                    item.Currency)
                .Select(BuildCurrencySummary)
                .OrderBy(summary =>
                    summary.Currency)
                .ToList();

        return new InvestmentAccrualReportDto
        {
            GeneratedAtUtc =
                DateTime.UtcNow,

            AsOfUtc =
                asOfUtc,

            CurrencyFilter =
                normalizedQuery.Currency,

            InstitutionFilter =
                normalizedQuery.InstitutionName,

            IncludesRedeemed =
                normalizedQuery.IncludeRedeemed,

            PlacementCount =
                items.Count,

            OutstandingPlacementCount =
                items.Count(item =>
                    item.IsOutstandingAsOf),

            RedeemedPlacementCount =
                items.Count(item =>
                    item.IsRedeemedAsOf),

            Currencies =
                currencySummaries,

            Items =
                items
        };
    }

    private static InvestmentAccrualItemDto MapAccrualItem(
        InvestmentPlacement placement,
        DateTime asOfUtc)
    {
        var startDateUtc =
            placement.StartDateUtc.Date;

        var maturityDateUtc =
            placement.MaturityDateUtc.Date;

        DateTime? redeemedDateUtc =
            placement.RedeemedAtUtc.HasValue
                ? NormalizeUtc(
                    placement.RedeemedAtUtc.Value).Date
                : null;

        var isRedeemedAsOf =
            placement.Status ==
                InvestmentPlacementStatuses.Redeemed &&
            (!redeemedDateUtc.HasValue ||
             redeemedDateUtc.Value <= asOfUtc);

        /*
         * If a currently redeemed placement was redeemed
         * after the selected date, it was still outstanding
         * on that selected date.
         */
        var isOutstandingAsOf =
            placement.Status ==
                InvestmentPlacementStatuses.Active ||
            placement.Status ==
                InvestmentPlacementStatuses.Matured ||
            (placement.Status ==
                InvestmentPlacementStatuses.Redeemed &&
             !isRedeemedAsOf);

        var accrualThroughUtc =
            asOfUtc;

        if (accrualThroughUtc > maturityDateUtc)
        {
            accrualThroughUtc =
                maturityDateUtc;
        }

        if (accrualThroughUtc < startDateUtc)
        {
            accrualThroughUtc =
                startDateUtc;
        }

        var contractDays =
            Math.Max(
                0,
                (maturityDateUtc -
                 startDateUtc).Days);

        var accruedDays =
            isOutstandingAsOf
                ? Math.Max(
                    0,
                    (accrualThroughUtc -
                     startDateUtc).Days)
                : 0;

        var accruedInterestAmount =
            isOutstandingAsOf
                ? CalculateAccruedInterest(
                    placement.PrincipalAmount,
                    placement.AnnualInterestRate,
                    placement.DayCountBasis,
                    accruedDays)
                : 0m;

        var carryingAmount =
            isOutstandingAsOf
                ? RoundMoney(
                    placement.PrincipalAmount +
                    accruedInterestAmount)
                : 0m;

        var actualInterestAmount =
            isRedeemedAsOf
                ? placement.ActualInterestAmount
                : 0m;

        var withholdingTaxAmount =
            isRedeemedAsOf
                ? placement.WithholdingTaxAmount
                : 0m;

        var realizedNetInterestAmount =
            RoundMoney(
                actualInterestAmount -
                withholdingTaxAmount);

        decimal? interestVarianceAmount =
            isRedeemedAsOf
                ? RoundMoney(
                    actualInterestAmount -
                    placement.ExpectedInterestAmount)
                : null;

        var realizedYield =
            CalculateRealizedAnnualizedYield(
                placement,
                redeemedDateUtc,
                isRedeemedAsOf);

        return new InvestmentAccrualItemDto
        {
            PlacementId =
                placement.Id,

            Reference =
                placement.Reference,

            InstitutionName =
                placement.InstitutionName,

            Currency =
                placement.Currency,

            Status =
                placement.Status,

            PrincipalAmount =
                placement.PrincipalAmount,

            AnnualInterestRate =
                placement.AnnualInterestRate,

            DayCountBasis =
                placement.DayCountBasis,

            StartDateUtc =
                placement.StartDateUtc,

            MaturityDateUtc =
                placement.MaturityDateUtc,

            AccrualThroughUtc =
                accrualThroughUtc,

            ContractDays =
                contractDays,

            AccruedDays =
                accruedDays,

            RemainingDays =
                Math.Max(
                    0,
                    (maturityDateUtc -
                     asOfUtc).Days),

            IsOutstandingAsOf =
                isOutstandingAsOf,

            IsRedeemedAsOf =
                isRedeemedAsOf,

            ExpectedInterestAmount =
                placement.ExpectedInterestAmount,

            AccruedInterestAmount =
                accruedInterestAmount,

            CarryingAmount =
                carryingAmount,

            ActualInterestAmount =
                actualInterestAmount,

            WithholdingTaxAmount =
                withholdingTaxAmount,

            RealizedNetInterestAmount =
                realizedNetInterestAmount,

            ActualRedemptionProceeds =
                isRedeemedAsOf
                    ? placement.ActualMaturityAmount
                    : 0m,

            InterestVarianceAmount =
                interestVarianceAmount,

            RealizedAnnualizedYieldPercentage =
                realizedYield,

            RedeemedAtUtc =
                placement.RedeemedAtUtc
        };
    }

    private static InvestmentAccrualCurrencySummaryDto
        BuildCurrencySummary(
            IGrouping<string, InvestmentAccrualItemDto> group)
    {
        var items =
            group.ToList();

        var outstanding =
            items
                .Where(item =>
                    item.IsOutstandingAsOf)
                .ToList();

        var redeemed =
            items
                .Where(item =>
                    item.IsRedeemedAsOf)
                .ToList();

        var outstandingPrincipal =
            outstanding.Sum(item =>
                item.PrincipalAmount);

        var weightedAverageRate =
            outstandingPrincipal <= 0
                ? 0m
                : outstanding.Sum(item =>
                    item.PrincipalAmount *
                    item.AnnualInterestRate) /
                  outstandingPrincipal;

        return new InvestmentAccrualCurrencySummaryDto
        {
            Currency =
                group.Key,

            PlacementCount =
                items.Count,

            OutstandingPlacementCount =
                outstanding.Count,

            RedeemedPlacementCount =
                redeemed.Count,

            OutstandingPrincipal =
                outstandingPrincipal,

            AccruedInterestAmount =
                outstanding.Sum(item =>
                    item.AccruedInterestAmount),

            CarryingAmount =
                outstanding.Sum(item =>
                    item.CarryingAmount),

            OutstandingExpectedInterestAmount =
                outstanding.Sum(item =>
                    item.ExpectedInterestAmount),

            RealizedGrossInterestAmount =
                redeemed.Sum(item =>
                    item.ActualInterestAmount),

            WithholdingTaxAmount =
                redeemed.Sum(item =>
                    item.WithholdingTaxAmount),

            RealizedNetInterestAmount =
                redeemed.Sum(item =>
                    item.RealizedNetInterestAmount),

            ActualRedemptionProceeds =
                redeemed.Sum(item =>
                    item.ActualRedemptionProceeds),

            InterestVarianceAmount =
                redeemed.Sum(item =>
                    item.InterestVarianceAmount ?? 0m),

            WeightedAverageContractRate =
                Math.Round(
                    weightedAverageRate,
                    6,
                    MidpointRounding.AwayFromZero)
        };
    }

    private static decimal CalculateAccruedInterest(
        decimal principalAmount,
        decimal annualInterestRate,
        int dayCountBasis,
        int accruedDays)
    {
        if (principalAmount <= 0 ||
            annualInterestRate <= 0 ||
            accruedDays <= 0)
        {
            return 0m;
        }

        var interest =
            principalAmount *
            (annualInterestRate / 100m) *
            accruedDays /
            dayCountBasis;

        return RoundMoney(interest);
    }

    private static decimal?
        CalculateRealizedAnnualizedYield(
            InvestmentPlacement placement,
            DateTime? redeemedDateUtc,
            bool isRedeemedAsOf)
    {
        if (!isRedeemedAsOf ||
            placement.PrincipalAmount <= 0)
        {
            return null;
        }

        var realizationDateUtc =
            redeemedDateUtc ??
            placement.MaturityDateUtc.Date;

        var daysHeld =
            Math.Max(
                1,
                (realizationDateUtc -
                 placement.StartDateUtc.Date).Days);

        var annualizedYield =
            placement.ActualInterestAmount /
            placement.PrincipalAmount *
            placement.DayCountBasis /
            daysHeld *
            100m;

        return Math.Round(
            annualizedYield,
            6,
            MidpointRounding.AwayFromZero);
    }

    private static InvestmentAccrualQueryDto NormalizeQuery(
        InvestmentAccrualQueryDto query)
    {
        var asOfUtc =
            query.AsOfUtc.HasValue
                ? NormalizeUtc(
                    query.AsOfUtc.Value).Date
                : DateTime.UtcNow.Date;

        if (asOfUtc > DateTime.UtcNow.Date)
        {
            throw new BusinessRuleException(
                "AsOfUtc cannot be a future date.");
        }

        string? currency =
            string.IsNullOrWhiteSpace(query.Currency)
                ? null
                : NormalizeCurrency(query.Currency);

        string? institutionName =
            string.IsNullOrWhiteSpace(
                query.InstitutionName)
                ? null
                : query.InstitutionName.Trim();

        if (institutionName?.Length > 200)
        {
            throw new BusinessRuleException(
                "Institution name cannot exceed 200 characters.");
        }

        return new InvestmentAccrualQueryDto
        {
            AsOfUtc =
                asOfUtc,

            Currency =
                currency,

            InstitutionName =
                institutionName,

            IncludeRedeemed =
                query.IncludeRedeemed
        };
    }

    private static string NormalizeCurrency(
        string currency)
    {
        var normalized =
            currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3 ||
            !normalized.All(char.IsLetter))
        {
            throw new BusinessRuleException(
                "Currency must be a valid three-letter code.");
        }

        return normalized;
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
        decimal amount)
    {
        return Math.Round(
            amount,
            2,
            MidpointRounding.AwayFromZero);
    }
}