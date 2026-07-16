using System.Text;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Exports;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;

namespace Treasury.Infrastructure.Services;

public class InvestmentAccrualSnapshotService
    : IInvestmentAccrualSnapshotService
{
    private readonly IInvestmentAccrualService
        _accrualService;

    private readonly IInvestmentAccrualSnapshotRepository
        _snapshotRepository;

    private readonly ICurrentUserService
        _currentUserService;

    public InvestmentAccrualSnapshotService(
        IInvestmentAccrualService accrualService,
        IInvestmentAccrualSnapshotRepository
            snapshotRepository,
        ICurrentUserService currentUserService)
    {
        _accrualService =
            accrualService;

        _snapshotRepository =
            snapshotRepository;

        _currentUserService =
            currentUserService;
    }

    public async Task<
        InvestmentAccrualSnapshotGenerationResultDto>
        Generate(
            GenerateInvestmentAccrualSnapshotsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var report =
            await _accrualService.GetReport(
                new InvestmentAccrualQueryDto
                {
                    AsOfUtc =
                        dto.SnapshotDateUtc,

                    Currency =
                        dto.Currency,

                    InstitutionName =
                        dto.InstitutionName,

                    IncludeRedeemed =
                        dto.IncludeRedeemed
                });

        var placementIds =
            report.Items
                .Select(item =>
                    item.PlacementId)
                .Distinct()
                .ToList();

        var existingPlacementIds =
            await _snapshotRepository
                .GetExistingPlacementIds(
                    report.AsOfUtc.Date,
                    placementIds);

        var createdAtUtc =
            DateTime.UtcNow;

        var createdByUserId =
            TryGetCurrentUserId();

        var snapshots =
            report.Items
                .Where(item =>
                    !existingPlacementIds.Contains(
                        item.PlacementId))
                .Select(item =>
                    new InvestmentAccrualSnapshot
                    {
                        Id =
                            Guid.NewGuid(),

                        InvestmentPlacementId =
                            item.PlacementId,

                        SnapshotDateUtc =
                            report.AsOfUtc.Date,

                        InvestmentReference =
                            item.Reference,

                        InstitutionName =
                            item.InstitutionName,

                        Currency =
                            item.Currency,

                        PlacementStatus =
                            item.Status,

                        PrincipalAmount =
                            item.PrincipalAmount,

                        AnnualInterestRate =
                            item.AnnualInterestRate,

                        DayCountBasis =
                            item.DayCountBasis,

                        AccruedDays =
                            item.AccruedDays,

                        ExpectedInterestAmount =
                            item.ExpectedInterestAmount,

                        AccruedInterestAmount =
                            item.AccruedInterestAmount,

                        CarryingAmount =
                            item.CarryingAmount,

                        IsOutstandingAsOf =
                            item.IsOutstandingAsOf,

                        IsRedeemedAsOf =
                            item.IsRedeemedAsOf,

                        ActualInterestAmount =
                            item.ActualInterestAmount,

                        WithholdingTaxAmount =
                            item.WithholdingTaxAmount,

                        RealizedNetInterestAmount =
                            item.RealizedNetInterestAmount,

                        ActualRedemptionProceeds =
                            item.ActualRedemptionProceeds,

                        InterestVarianceAmount =
                            item.InterestVarianceAmount,

                        RealizedAnnualizedYieldPercentage =
                            item
                                .RealizedAnnualizedYieldPercentage,

                        CreatedByUserId =
                            createdByUserId,

                        CreatedAtUtc =
                            createdAtUtc
                    })
                .ToList();

        if (snapshots.Count > 0)
        {
            await _snapshotRepository.AddRange(
                snapshots);

            await _snapshotRepository.SaveChanges();
        }

        return new
            InvestmentAccrualSnapshotGenerationResultDto
            {
                SnapshotDateUtc =
                    report.AsOfUtc.Date,

                EligiblePlacementCount =
                    report.Items.Count,

                CreatedSnapshotCount =
                    snapshots.Count,

                SkippedDuplicateCount =
                    report.Items.Count -
                    snapshots.Count,

                CreatedSnapshots =
                    snapshots
                        .Select(Map)
                        .ToList()
            };
    }

    public async Task<
        PagedInvestmentAccrualSnapshotResponseDto>
        Search(
            InvestmentAccrualSnapshotQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalizedQuery =
            NormalizeQuery(query);

        var result =
            await _snapshotRepository.Search(
                normalizedQuery);

        return new
            PagedInvestmentAccrualSnapshotResponseDto
            {
                Items =
                    result.Items
                        .Select(Map)
                        .ToList(),

                Page =
                    normalizedQuery.Page,

                PageSize =
                    normalizedQuery.PageSize,

                TotalCount =
                    result.TotalCount,

                TotalPages =
                    (int)Math.Ceiling(
                        result.TotalCount /
                        (double)normalizedQuery.PageSize)
            };
    }

    public async Task<CsvExportDto> ExportCsv(
        InvestmentAccrualSnapshotQueryDto query,
        int maxRows = 5000)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (maxRows < 1 ||
            maxRows > 10_000)
        {
            throw new BusinessRuleException(
                "Maximum export rows must be between " +
                "1 and 10000.");
        }

        var normalizedQuery =
            NormalizeQuery(query);

        var snapshots =
            await _snapshotRepository.GetForExport(
                normalizedQuery,
                maxRows);

        var csv =
            new StringBuilder();

        csv.AppendLine(
            "SnapshotId," +
            "InvestmentPlacementId," +
            "SnapshotDateUtc," +
            "InvestmentReference," +
            "InstitutionName," +
            "Currency," +
            "PlacementStatus," +
            "PrincipalAmount," +
            "AnnualInterestRate," +
            "DayCountBasis," +
            "AccruedDays," +
            "ExpectedInterestAmount," +
            "AccruedInterestAmount," +
            "CarryingAmount," +
            "IsOutstandingAsOf," +
            "IsRedeemedAsOf," +
            "ActualInterestAmount," +
            "WithholdingTaxAmount," +
            "RealizedNetInterestAmount," +
            "ActualRedemptionProceeds," +
            "InterestVarianceAmount," +
            "RealizedAnnualizedYieldPercentage," +
            "CreatedByUserId," +
            "CreatedAtUtc");

        foreach (var snapshot in snapshots)
        {
            csv.AppendLine(
                string.Join(
                    ",",
                    CsvExportHelper.Escape(
                        snapshot.Id),

                    CsvExportHelper.Escape(
                        snapshot.InvestmentPlacementId),

                    CsvExportHelper.Escape(
                        snapshot.SnapshotDateUtc),

                    CsvExportHelper.Escape(
                        snapshot.InvestmentReference),

                    CsvExportHelper.Escape(
                        snapshot.InstitutionName),

                    CsvExportHelper.Escape(
                        snapshot.Currency),

                    CsvExportHelper.Escape(
                        snapshot.PlacementStatus),

                    CsvExportHelper.Escape(
                        snapshot.PrincipalAmount),

                    CsvExportHelper.Escape(
                        snapshot.AnnualInterestRate),

                    CsvExportHelper.Escape(
                        snapshot.DayCountBasis),

                    CsvExportHelper.Escape(
                        snapshot.AccruedDays),

                    CsvExportHelper.Escape(
                        snapshot.ExpectedInterestAmount),

                    CsvExportHelper.Escape(
                        snapshot.AccruedInterestAmount),

                    CsvExportHelper.Escape(
                        snapshot.CarryingAmount),

                    CsvExportHelper.Escape(
                        snapshot.IsOutstandingAsOf),

                    CsvExportHelper.Escape(
                        snapshot.IsRedeemedAsOf),

                    CsvExportHelper.Escape(
                        snapshot.ActualInterestAmount),

                    CsvExportHelper.Escape(
                        snapshot.WithholdingTaxAmount),

                    CsvExportHelper.Escape(
                        snapshot.RealizedNetInterestAmount),

                    CsvExportHelper.Escape(
                        snapshot.ActualRedemptionProceeds),

                    CsvExportHelper.Escape(
                        snapshot.InterestVarianceAmount),

                    CsvExportHelper.Escape(
                        snapshot
                            .RealizedAnnualizedYieldPercentage),

                    CsvExportHelper.Escape(
                        snapshot.CreatedByUserId),

                    CsvExportHelper.Escape(
                        snapshot.CreatedAtUtc)));
        }

        return new CsvExportDto
        {
            FileName =
                $"investment-accrual-snapshots-" +
                $"{DateTime.UtcNow:yyyyMMddHHmmss}.csv",

            ContentType =
                "text/csv; charset=utf-8",

            Content =
                CsvExportHelper.ToUtf8Bytes(
                    csv.ToString())
        };
    }

    private static InvestmentAccrualSnapshotQueryDto
        NormalizeQuery(
            InvestmentAccrualSnapshotQueryDto query)
    {
        DateTime? snapshotDateFromUtc =
            query.SnapshotDateFromUtc.HasValue
                ? NormalizeUtc(
                    query.SnapshotDateFromUtc.Value).Date
                : null;

        DateTime? snapshotDateToUtc =
            query.SnapshotDateToUtc.HasValue
                ? NormalizeUtc(
                    query.SnapshotDateToUtc.Value).Date
                : null;

        if (snapshotDateFromUtc.HasValue &&
            snapshotDateToUtc.HasValue &&
            snapshotDateFromUtc.Value >
                snapshotDateToUtc.Value)
        {
            throw new BusinessRuleException(
                "SnapshotDateFromUtc cannot be later " +
                "than SnapshotDateToUtc.");
        }

        if (query.Page < 1)
        {
            throw new BusinessRuleException(
                "Page must be at least 1.");
        }

        if (query.PageSize < 1 ||
            query.PageSize > 200)
        {
            throw new BusinessRuleException(
                "Page size must be between 1 and 200.");
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

        return new InvestmentAccrualSnapshotQueryDto
        {
            SnapshotDateFromUtc =
                snapshotDateFromUtc,

            SnapshotDateToUtc =
                snapshotDateToUtc,

            Currency =
                currency,

            InstitutionName =
                institutionName,

            InvestmentPlacementId =
                query.InvestmentPlacementId,

            Page =
                query.Page,

            PageSize =
                query.PageSize
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

    private Guid? TryGetCurrentUserId()
    {
        try
        {
            var userId =
                _currentUserService.UserId;

            return userId == Guid.Empty
                ? null
                : userId;
        }
        catch
        {
            /*
            * Background services do not have an
            * authenticated HTTP context.
            */
            return null;
        }
    }

    private static
        InvestmentAccrualSnapshotResponseDto Map(
            InvestmentAccrualSnapshot snapshot)
    {
        return new InvestmentAccrualSnapshotResponseDto
        {
            Id =
                snapshot.Id,

            InvestmentPlacementId =
                snapshot.InvestmentPlacementId,

            SnapshotDateUtc =
                snapshot.SnapshotDateUtc,

            InvestmentReference =
                snapshot.InvestmentReference,

            InstitutionName =
                snapshot.InstitutionName,

            Currency =
                snapshot.Currency,

            PlacementStatus =
                snapshot.PlacementStatus,

            PrincipalAmount =
                snapshot.PrincipalAmount,

            AnnualInterestRate =
                snapshot.AnnualInterestRate,

            DayCountBasis =
                snapshot.DayCountBasis,

            AccruedDays =
                snapshot.AccruedDays,

            ExpectedInterestAmount =
                snapshot.ExpectedInterestAmount,

            AccruedInterestAmount =
                snapshot.AccruedInterestAmount,

            CarryingAmount =
                snapshot.CarryingAmount,

            IsOutstandingAsOf =
                snapshot.IsOutstandingAsOf,

            IsRedeemedAsOf =
                snapshot.IsRedeemedAsOf,

            ActualInterestAmount =
                snapshot.ActualInterestAmount,

            WithholdingTaxAmount =
                snapshot.WithholdingTaxAmount,

            RealizedNetInterestAmount =
                snapshot.RealizedNetInterestAmount,

            ActualRedemptionProceeds =
                snapshot.ActualRedemptionProceeds,

            InterestVarianceAmount =
                snapshot.InterestVarianceAmount,

            RealizedAnnualizedYieldPercentage =
                snapshot
                    .RealizedAnnualizedYieldPercentage,

            CreatedByUserId =
                snapshot.CreatedByUserId,

            CreatedAtUtc =
                snapshot.CreatedAtUtc
        };
    }
}