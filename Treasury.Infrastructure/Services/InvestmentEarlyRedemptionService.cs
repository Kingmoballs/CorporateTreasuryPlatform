using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class InvestmentEarlyRedemptionService
    : IInvestmentEarlyRedemptionService
{
    private readonly IInvestmentPlacementRepository
        _placementRepository;

    public InvestmentEarlyRedemptionService(
        IInvestmentPlacementRepository placementRepository)
    {
        _placementRepository =
            placementRepository;
    }

    public async Task<InvestmentEarlyRedemptionQuoteDto>
        GetQuote(
            Guid investmentPlacementId,
            InvestmentEarlyRedemptionQuoteRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (investmentPlacementId == Guid.Empty)
        {
            throw new RequestValidationException(
                "A valid investment placement ID is required.");
        }

        ValidateRates(request);

        var placement =
            await _placementRepository.GetById(
                investmentPlacementId);

        if (placement is null)
        {
            throw new ResourceNotFoundException(
                "Investment placement was not found.");
        }

        if (placement.Status !=
            InvestmentPlacementStatuses.Active)
        {
            throw new ConflictException(
                "Only an active investment placement " +
                "can receive an early-redemption quote.");
        }

        var todayUtc =
            DateTime.UtcNow.Date;

        var proposedRedemptionDateUtc =
            request.ProposedRedemptionDateUtc.HasValue
                ? NormalizeUtc(
                    request.ProposedRedemptionDateUtc.Value)
                    .Date
                : todayUtc;

        var startDateUtc =
            placement.StartDateUtc.Date;

        var maturityDateUtc =
            placement.MaturityDateUtc.Date;

        if (proposedRedemptionDateUtc < todayUtc)
        {
            throw new BusinessRuleException(
                "The proposed redemption date cannot " +
                "be earlier than today.");
        }

        if (proposedRedemptionDateUtc < startDateUtc)
        {
            throw new BusinessRuleException(
                "The proposed redemption date cannot " +
                "be earlier than the investment start date.");
        }

        if (proposedRedemptionDateUtc >=
            maturityDateUtc)
        {
            throw new BusinessRuleException(
                "The proposed date is not an early " +
                "redemption date. Use the normal maturity " +
                "redemption process on or after maturity.");
        }

        var investedDays =
            (proposedRedemptionDateUtc -
             startDateUtc).Days;

        var remainingDays =
            (maturityDateUtc -
             proposedRedemptionDateUtc).Days;

        var grossAccruedInterest =
            RoundMoney(
                placement.PrincipalAmount *
                (placement.AnnualInterestRate / 100m) *
                investedDays /
                placement.DayCountBasis);

        /*
         * The penalty is applied to accrued interest,
         * not to the investment principal.
         */
        var penaltyAmount =
            RoundMoney(
                grossAccruedInterest *
                (request.PenaltyRatePercentage / 100m));

        var interestAfterPenalty =
            RoundMoney(
                grossAccruedInterest -
                penaltyAmount);

        var withholdingTaxAmount =
            RoundMoney(
                interestAfterPenalty *
                (request
                    .WithholdingTaxRatePercentage /
                 100m));

        var netInterestAmount =
            RoundMoney(
                interestAfterPenalty -
                withholdingTaxAmount);

        var estimatedRedemptionProceeds =
            RoundMoney(
                placement.PrincipalAmount +
                netInterestAmount);

        var expectedProceedsShortfall =
            RoundMoney(
                Math.Max(
                    0m,
                    placement.ExpectedMaturityAmount -
                    estimatedRedemptionProceeds));

        return new InvestmentEarlyRedemptionQuoteDto
        {
            InvestmentPlacementId =
                placement.Id,

            InvestmentReference =
                placement.Reference,

            InstitutionName =
                placement.InstitutionName,

            Currency =
                placement.Currency,

            StartDateUtc =
                placement.StartDateUtc,

            OriginalMaturityDateUtc =
                placement.MaturityDateUtc,

            ProposedRedemptionDateUtc =
                proposedRedemptionDateUtc,

            InvestedDays =
                investedDays,

            RemainingDays =
                remainingDays,

            PrincipalAmount =
                placement.PrincipalAmount,

            AnnualInterestRate =
                placement.AnnualInterestRate,

            DayCountBasis =
                placement.DayCountBasis,

            PenaltyRatePercentage =
                request.PenaltyRatePercentage,

            WithholdingTaxRatePercentage =
                request.WithholdingTaxRatePercentage,

            GrossAccruedInterestAmount =
                grossAccruedInterest,

            PenaltyAmount =
                penaltyAmount,

            InterestAfterPenaltyAmount =
                interestAfterPenalty,

            WithholdingTaxAmount =
                withholdingTaxAmount,

            NetInterestAmount =
                netInterestAmount,

            EstimatedRedemptionProceeds =
                estimatedRedemptionProceeds,

            OriginalExpectedMaturityAmount =
                placement.ExpectedMaturityAmount,

            ExpectedProceedsShortfall =
                expectedProceedsShortfall,

            GeneratedAtUtc =
                DateTime.UtcNow
        };
    }

    private static void ValidateRates(
        InvestmentEarlyRedemptionQuoteRequestDto request)
    {
        if (request.PenaltyRatePercentage < 0 ||
            request.PenaltyRatePercentage > 100)
        {
            throw new BusinessRuleException(
                "Penalty rate percentage must be " +
                "between 0 and 100.");
        }

        if (request.WithholdingTaxRatePercentage < 0 ||
            request.WithholdingTaxRatePercentage > 100)
        {
            throw new BusinessRuleException(
                "Withholding tax rate percentage must " +
                "be between 0 and 100.");
        }
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