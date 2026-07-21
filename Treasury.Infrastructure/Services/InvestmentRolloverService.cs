using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class InvestmentRolloverService
    : IInvestmentRolloverService
{
    private readonly IInvestmentPlacementRepository
        _placementRepository;

    public InvestmentRolloverService(
        IInvestmentPlacementRepository placementRepository)
    {
        _placementRepository = placementRepository;
    }

    public async Task<InvestmentRolloverQuoteDto> GetQuote(
        Guid investmentPlacementId,
        InvestmentRolloverQuoteRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (investmentPlacementId == Guid.Empty)
        {
            throw new RequestValidationException(
                "A valid investment placement ID is required.");
        }

        ValidateRequest(request);

        var placement =
            await _placementRepository.GetById(
                investmentPlacementId);

        if (placement is null)
        {
            throw new ResourceNotFoundException(
                "Investment placement was not found.");
        }

        /*
         * Active placements can receive planning quotes.
         * Matured placements can receive executable quotes.
         */
        if (placement.Status !=
                InvestmentPlacementStatuses.Active &&
            placement.Status !=
                InvestmentPlacementStatuses.Matured)
        {
            throw new ConflictException(
                "Only an active or matured investment " +
                "placement can receive a rollover quote.");
        }

        var rolloverOption =
            NormalizeRolloverOption(
                request.RolloverOption);

        var newInvestmentType =
            NormalizeInvestmentType(
                request.NewInvestmentType ??
                placement.InvestmentType);

        var newInstitutionName =
            placement.InstitutionName;

        if (!string.IsNullOrWhiteSpace(
                request.NewInstitutionName))
        {
            var requestedInstitutionName =
                NormalizeInstitutionName(
                    request.NewInstitutionName);

            if (!string.Equals(
                    requestedInstitutionName,
                    placement.InstitutionName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException(
                    "A rollover must remain with the original " +
                    "counterparty. Redeem the investment and " +
                    "create a new placement to move funds to " +
                    "another counterparty.");
            }
        }

        var todayUtc =
            DateTime.UtcNow.Date;

        var originalMaturityDateUtc =
            placement.MaturityDateUtc.Date;

        var defaultStartDateUtc =
            originalMaturityDateUtc > todayUtc
                ? originalMaturityDateUtc
                : todayUtc;

        var newStartDateUtc =
            request.NewStartDateUtc.HasValue
                ? NormalizeUtc(
                    request.NewStartDateUtc.Value).Date
                : defaultStartDateUtc;

        var newMaturityDateUtc =
            NormalizeUtc(
                request.NewMaturityDateUtc).Date;

        ValidateDates(
            originalMaturityDateUtc,
            todayUtc,
            newStartDateUtc,
            newMaturityDateUtc);

        var newTenorDays =
            (newMaturityDateUtc -
             newStartDateUtc).Days;

        var grossInterestAmount =
            RoundMoney(
                request.GrossInterestAmount ??
                placement.ExpectedInterestAmount);

        if (grossInterestAmount < 0)
        {
            throw new BusinessRuleException(
                "Gross interest amount cannot be negative.");
        }

        /*
         * Withholding tax is calculated on interest only,
         * never on the investment principal.
         */
        var withholdingTaxAmount =
            RoundMoney(
                grossInterestAmount *
                (request
                    .WithholdingTaxRatePercentage /
                 100m));

        var netInterestAmount =
            RoundMoney(
                grossInterestAmount -
                withholdingTaxAmount);

        var grossMaturityAmount =
            RoundMoney(
                placement.PrincipalAmount +
                grossInterestAmount);

        var netMaturityProceeds =
            RoundMoney(
                placement.PrincipalAmount +
                netInterestAmount);

        decimal rolloverPrincipalAmount;
        decimal cashPayoutAmount;

        if (rolloverOption ==
            InvestmentRolloverOptions.PrincipalOnly)
        {
            rolloverPrincipalAmount =
                RoundMoney(
                    placement.PrincipalAmount);

            cashPayoutAmount =
                netInterestAmount;
        }
        else
        {
            rolloverPrincipalAmount =
                netMaturityProceeds;

            cashPayoutAmount =
                0m;
        }

        /*
         * New expected interest:
         * Principal × annual rate × tenor ÷ basis.
         */
        var newExpectedInterestAmount =
            RoundMoney(
                rolloverPrincipalAmount *
                (request.NewAnnualInterestRate /
                 100m) *
                newTenorDays /
                request.NewDayCountBasis);

        var newExpectedMaturityAmount =
            RoundMoney(
                rolloverPrincipalAmount +
                newExpectedInterestAmount);

        return new InvestmentRolloverQuoteDto
        {
            OriginalInvestmentPlacementId =
                placement.Id,

            OriginalInvestmentReference =
                placement.Reference,

            OriginalInvestmentStatus =
                placement.Status,

            OriginalInvestmentType =
                placement.InvestmentType,

            OriginalInstitutionName =
                placement.InstitutionName,

            Currency =
                placement.Currency,

            OriginalMaturityDateUtc =
                originalMaturityDateUtc,

            OriginalPrincipalAmount =
                placement.PrincipalAmount,

            GrossInterestAmount =
                grossInterestAmount,

            GrossMaturityAmount =
                grossMaturityAmount,

            WithholdingTaxRatePercentage =
                request.WithholdingTaxRatePercentage,

            WithholdingTaxAmount =
                withholdingTaxAmount,

            NetInterestAmount =
                netInterestAmount,

            NetMaturityProceeds =
                netMaturityProceeds,

            RolloverOption =
                rolloverOption,

            RolloverPrincipalAmount =
                rolloverPrincipalAmount,

            CashPayoutAmount =
                cashPayoutAmount,

            NewInvestmentType =
                newInvestmentType,

            NewInstitutionName =
                newInstitutionName,

            NewAnnualInterestRate =
                request.NewAnnualInterestRate,

            NewDayCountBasis =
                request.NewDayCountBasis,

            NewStartDateUtc =
                newStartDateUtc,

            NewMaturityDateUtc =
                newMaturityDateUtc,

            NewTenorDays =
                newTenorDays,

            NewExpectedInterestAmount =
                newExpectedInterestAmount,

            NewExpectedMaturityAmount =
                newExpectedMaturityAmount,

            CanExecuteNow =
                originalMaturityDateUtc <= todayUtc,

            GeneratedAtUtc =
                DateTime.UtcNow
        };
    }

    private static void ValidateRequest(
        InvestmentRolloverQuoteRequestDto request)
    {
        if (request.GrossInterestAmount.HasValue &&
            request.GrossInterestAmount.Value < 0)
        {
            throw new BusinessRuleException(
                "Gross interest amount cannot be negative.");
        }

        if (request.WithholdingTaxRatePercentage < 0 ||
            request.WithholdingTaxRatePercentage > 100)
        {
            throw new BusinessRuleException(
                "Withholding tax rate percentage must " +
                "be between 0 and 100.");
        }

        if (request.NewAnnualInterestRate < 0 ||
            request.NewAnnualInterestRate > 100)
        {
            throw new BusinessRuleException(
                "New annual interest rate must be " +
                "between 0 and 100.");
        }

        if (request.NewDayCountBasis != 360 &&
            request.NewDayCountBasis != 365)
        {
            throw new BusinessRuleException(
                "New day-count basis must be either " +
                "360 or 365.");
        }

        if (request.NewMaturityDateUtc == default)
        {
            throw new RequestValidationException(
                "New maturity date is required.");
        }
    }

    private static void ValidateDates(
        DateTime originalMaturityDateUtc,
        DateTime todayUtc,
        DateTime newStartDateUtc,
        DateTime newMaturityDateUtc)
    {
        if (newStartDateUtc <
            originalMaturityDateUtc)
        {
            throw new BusinessRuleException(
                "The new investment start date cannot " +
                "be earlier than the original maturity date.");
        }

        if (newStartDateUtc < todayUtc)
        {
            throw new BusinessRuleException(
                "The new investment start date cannot " +
                "be earlier than today.");
        }

        if (newMaturityDateUtc <=
            newStartDateUtc)
        {
            throw new BusinessRuleException(
                "The new maturity date must be later " +
                "than the new start date.");
        }

        var tenorDays =
            (newMaturityDateUtc -
             newStartDateUtc).Days;

        if (tenorDays > 3650)
        {
            throw new BusinessRuleException(
                "The new investment tenor cannot exceed " +
                "10 years.");
        }
    }

    private static string NormalizeRolloverOption(
        string? value)
    {
        var normalized =
            value?.Trim();

        if (string.Equals(
            normalized,
            InvestmentRolloverOptions.PrincipalOnly,
            StringComparison.OrdinalIgnoreCase))
        {
            return InvestmentRolloverOptions.PrincipalOnly;
        }

        if (string.Equals(
            normalized,
            InvestmentRolloverOptions
                .PrincipalAndNetInterest,
            StringComparison.OrdinalIgnoreCase))
        {
            return InvestmentRolloverOptions
                .PrincipalAndNetInterest;
        }

        throw new BusinessRuleException(
            "Rollover option must be PrincipalOnly or " +
            "PrincipalAndNetInterest.");
    }

    private static string NormalizeInvestmentType(
        string? value)
    {
        if (string.Equals(
            value?.Trim(),
            InvestmentPlacementTypes.FixedDeposit,
            StringComparison.OrdinalIgnoreCase))
        {
            return InvestmentPlacementTypes.FixedDeposit;
        }

        throw new BusinessRuleException(
            "New investment type must be FixedDeposit.");
    }

    private static string NormalizeInstitutionName(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RequestValidationException(
                "New institution name is required.");
        }

        var normalized =
            value.Trim();

        if (normalized.Length > 200)
        {
            throw new RequestValidationException(
                "New institution name cannot exceed " +
                "200 characters.");
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