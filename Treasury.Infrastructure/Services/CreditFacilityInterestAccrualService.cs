using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.CreditFacilityAccruals;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class CreditFacilityInterestAccrualService
    : ICreditFacilityInterestAccrualService
{
    private readonly ICreditFacilityRepository
        _facilityRepository;

    private readonly
        ICreditFacilityInterestAccrualSnapshotRepository
        _snapshotRepository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuditLogService
        _auditLogService;

    public CreditFacilityInterestAccrualService(
        ICreditFacilityRepository facilityRepository,
        ICreditFacilityInterestAccrualSnapshotRepository
            snapshotRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _facilityRepository =
            facilityRepository;

        _snapshotRepository =
            snapshotRepository;

        _currentUserService =
            currentUserService;

        _auditLogService =
            auditLogService;
    }

    public async Task<
        CreditFacilityAccrualGenerationResultDto>
        Generate(
            GenerateCreditFacilityAccrualsDto dto)
    {
        ValidateGenerationRequest(dto);

        var asOfDateUtc =
            NormalizeUtc(
                dto.AsOfDateUtc ??
                DateTime.UtcNow).Date;

        if (asOfDateUtc >
            DateTime.UtcNow.Date)
        {
            throw new BusinessRuleException(
                "Interest cannot be accrued for a future date.");
        }

        var facilities =
            await _facilityRepository
                .GetForInterestAccrual(
                    dto.CreditFacilityId,
                    asOfDateUtc,
                    dto.MaxFacilities);

        if (dto.CreditFacilityId.HasValue &&
            facilities.Count == 0)
        {
            var facility =
                await _facilityRepository.GetById(
                    dto.CreditFacilityId.Value);

            if (facility is null)
            {
                throw new ResourceNotFoundException(
                    "Credit facility was not found.");
            }
        }

        var result =
            new CreditFacilityAccrualGenerationResultDto
            {
                AsOfDateUtc =
                    asOfDateUtc,

                FacilitiesSelected =
                    facilities.Count
            };

        var allSnapshots =
            new List<
                CreditFacilityInterestAccrualSnapshot>();

        var auditItems =
            new List<(
                CreditFacility Facility,
                CreditFacilityAccrualGenerationItemDto Item,
                Guid LastSnapshotId)>();

        foreach (var facility in facilities)
        {
            var lastSnapshotDate =
                await _snapshotRepository
                    .GetLatestSnapshotDate(
                        facility.Id);

            var toExclusiveUtc =
                asOfDateUtc.AddDays(1);

            var drawdowns =
                await _snapshotRepository
                    .GetDrawdowns(
                        facility.Id,
                        toExclusiveUtc);

            if (drawdowns.Count == 0)
            {
                result.FacilitiesSkipped += 1;
                continue;
            }

            var earliestDrawdownDate =
                drawdowns.Min(drawdown =>
                    drawdown.DrawdownDateUtc).Date;

            var firstAccrualDate =
                lastSnapshotDate.HasValue
                    ? lastSnapshotDate.Value.Date
                        .AddDays(1)
                    : LaterDate(
                        facility.StartDateUtc.Date,
                        earliestDrawdownDate);

            /*
             * Interest does not continue beyond the
             * contractual maturity date.
             */
            var finalAccrualDate =
                EarlierDate(
                    asOfDateUtc,
                    facility.MaturityDateUtc.Date);

            if (firstAccrualDate >
                finalAccrualDate)
            {
                result.FacilitiesSkipped += 1;
                continue;
            }

            var daysToProcess =
                (finalAccrualDate -
                 firstAccrualDate).Days + 1;

            if (daysToProcess >
                dto.MaxAccrualDaysPerFacility)
            {
                throw new BusinessRuleException(
                    $"Facility {facility.Reference} requires " +
                    $"{daysToProcess} accrual days, exceeding " +
                    $"the configured limit of " +
                    $"{dto.MaxAccrualDaysPerFacility}.");
            }

            var repayments =
                await _snapshotRepository
                    .GetRepayments(
                        facility.Id,
                        finalAccrualDate.AddDays(1));

            var interestBeforeBatch =
                facility.AccruedInterestAmount;

            var runningAccruedInterest =
                interestBeforeBatch;

            var facilitySnapshots =
                new List<
                    CreditFacilityInterestAccrualSnapshot>();

            /*
             * End-of-day convention:
             * drawdowns and principal repayments dated on
             * a day affect that day's principal balance.
             */
            for (var date = firstAccrualDate;
                 date <= finalAccrualDate;
                 date = date.AddDays(1))
            {
                var dateEndExclusive =
                    date.AddDays(1);

                var totalDrawn =
                    drawdowns
                        .Where(drawdown =>
                            drawdown.DrawdownDateUtc <
                                dateEndExclusive)
                        .Sum(drawdown =>
                            drawdown.Amount);

                var totalPrincipalRepaid =
                    repayments
                        .Where(repayment =>
                            repayment.RepaymentDateUtc <
                                dateEndExclusive)
                        .Sum(repayment =>
                            repayment.PrincipalAmount);

                var outstandingPrincipal =
                    Math.Max(
                        0m,
                        totalDrawn -
                        totalPrincipalRepaid);

                outstandingPrincipal =
                    Math.Round(
                        outstandingPrincipal,
                        2,
                        MidpointRounding.AwayFromZero);

                var dailyInterest =
                    CalculateInterest(
                        outstandingPrincipal,
                        facility.AnnualInterestRate,
                        accruedDays: 1,
                        facility.DayCountBasis);

                var interestBefore =
                    runningAccruedInterest;

                runningAccruedInterest =
                    Math.Round(
                        runningAccruedInterest +
                        dailyInterest,
                        2,
                        MidpointRounding.AwayFromZero);

                facilitySnapshots.Add(
                    new CreditFacilityInterestAccrualSnapshot
                    {
                        Id =
                            Guid.NewGuid(),

                        CreditFacilityId =
                            facility.Id,

                        CreditFacility =
                            facility,

                        SnapshotDateUtc =
                            date,

                        FacilityReference =
                            facility.Reference,

                        FacilityName =
                            facility.FacilityName,

                        LenderName =
                            facility.LenderName,

                        Currency =
                            facility.Currency,

                        FacilityStatus =
                            facility.Status,

                        OutstandingPrincipalAmount =
                            outstandingPrincipal,

                        AnnualInterestRate =
                            facility.AnnualInterestRate,

                        DayCountBasis =
                            facility.DayCountBasis,

                        AccruedDays =
                            1,

                        AccruedInterestBefore =
                            interestBefore,

                        AccruedInterestAmount =
                            dailyInterest,

                        AccruedInterestAfter =
                            runningAccruedInterest,

                        CreatedByUserId =
                            _currentUserService.UserId,

                        CreatedAtUtc =
                            DateTime.UtcNow
                    });
            }

            var interestAccrued =
                runningAccruedInterest -
                interestBeforeBatch;

            facility.AccruedInterestAmount =
                runningAccruedInterest;

            facility.UpdatedByUserId =
                _currentUserService.UserId;

            facility.UpdatedAtUtc =
                DateTime.UtcNow;

            facility.ConcurrencyToken =
                Guid.NewGuid();

            _facilityRepository.Update(facility);

            allSnapshots.AddRange(
                facilitySnapshots);

            var item =
                new CreditFacilityAccrualGenerationItemDto
                {
                    CreditFacilityId =
                        facility.Id,

                    FacilityReference =
                        facility.Reference,

                    Currency =
                        facility.Currency,

                    FirstSnapshotDateUtc =
                        facilitySnapshots
                            .First().SnapshotDateUtc,

                    LastSnapshotDateUtc =
                        facilitySnapshots
                            .Last().SnapshotDateUtc,

                    SnapshotsCreated =
                        facilitySnapshots.Count,

                    AccruedInterestBefore =
                        interestBeforeBatch,

                    InterestAccrued =
                        interestAccrued,

                    AccruedInterestAfter =
                        runningAccruedInterest
                };

            result.Items.Add(item);

            result.FacilitiesProcessed += 1;

            result.SnapshotsCreated +=
                facilitySnapshots.Count;

            result.TotalInterestAccrued +=
                interestAccrued;

            auditItems.Add(
                (
                    facility,
                    item,
                    facilitySnapshots.Last().Id
                ));
        }

        if (allSnapshots.Count == 0)
        {
            return result;
        }

        await _snapshotRepository.AddRange(
            allSnapshots);

        try
        {
            /*
             * Snapshots and facility interest updates use
             * one DbContext and one atomic SaveChanges.
             */
            await _snapshotRepository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "A credit facility changed while interest " +
                "accrual was processing. Refresh and retry.");
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "Interest accrual could not be saved. One " +
                "or more facility dates may already have " +
                "been processed.");
        }

        foreach (var auditItem in auditItems)
        {
            await RecordAccrualAudit(
                auditItem.Facility,
                auditItem.Item,
                auditItem.LastSnapshotId);
        }

        result.TotalInterestAccrued =
            Math.Round(
                result.TotalInterestAccrued,
                2,
                MidpointRounding.AwayFromZero);

        return result;
    }

    public async Task<
        PagedCreditFacilityAccrualSnapshotResponseDto>
        Search(
            CreditFacilityAccrualSnapshotQueryDto query)
    {
        query.Page =
            query.Page < 1 ? 1 : query.Page;

        query.PageSize =
            query.PageSize < 1
                ? 50
                : Math.Min(query.PageSize, 100);

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            query.Currency =
                query.Currency.Trim()
                    .ToUpperInvariant();

            if (query.Currency.Length != 3)
            {
                throw new BusinessRuleException(
                    "Currency must contain three characters.");
            }
        }

        if (query.SnapshotDateFromUtc.HasValue)
        {
            query.SnapshotDateFromUtc =
                NormalizeUtc(
                    query.SnapshotDateFromUtc.Value)
                .Date;
        }

        if (query.SnapshotDateToUtc.HasValue)
        {
            query.SnapshotDateToUtc =
                NormalizeUtc(
                    query.SnapshotDateToUtc.Value)
                .Date;
        }

        if (query.SnapshotDateFromUtc.HasValue &&
            query.SnapshotDateToUtc.HasValue &&
            query.SnapshotDateFromUtc.Value >
                query.SnapshotDateToUtc.Value)
        {
            throw new BusinessRuleException(
                "Snapshot-from date cannot be later " +
                "than snapshot-to date.");
        }

        var result =
            await _snapshotRepository.Search(query);

        return new PagedCreditFacilityAccrualSnapshotResponseDto
        {
            Items =
                result.Items.Select(Map).ToList(),

            Page =
                query.Page,

            PageSize =
                query.PageSize,

            TotalCount =
                result.TotalCount,

            TotalPages =
                (int)Math.Ceiling(
                    result.TotalCount /
                    (double)query.PageSize)
        };
    }

    private static decimal CalculateInterest(
        decimal principal,
        decimal annualInterestRate,
        int accruedDays,
        int dayCountBasis)
    {
        if (principal <= 0 ||
            annualInterestRate <= 0)
        {
            return 0m;
        }

        return Math.Round(
            principal *
            (annualInterestRate / 100m) *
            accruedDays /
            dayCountBasis,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static void ValidateGenerationRequest(
        GenerateCreditFacilityAccrualsDto dto)
    {
        if (dto.CreditFacilityId.HasValue &&
            dto.CreditFacilityId.Value == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Credit facility ID is invalid.");
        }

        if (dto.MaxFacilities < 1 ||
            dto.MaxFacilities > 1000)
        {
            throw new BusinessRuleException(
                "Max facilities must be between 1 and 1000.");
        }

        if (dto.MaxAccrualDaysPerFacility < 1 ||
            dto.MaxAccrualDaysPerFacility > 366)
        {
            throw new BusinessRuleException(
                "Maximum accrual days per facility must " +
                "be between 1 and 366.");
        }
    }

    private async Task RecordAccrualAudit(
        CreditFacility facility,
        CreditFacilityAccrualGenerationItemDto item,
        Guid lastSnapshotId)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Accrued,

                EntityType =
                    AuditEntityTypes
                        .CreditFacilityInterestAccrualSnapshot,

                EntityId =
                    lastSnapshotId,

                EntityReference =
                    facility.Reference,

                Summary =
                    $"{item.InterestAccrued:N2} " +
                    $"{facility.Currency} interest was " +
                    $"accrued for facility " +
                    $"{facility.Reference}.",

                AfterValues =
                    new
                    {
                        item.CreditFacilityId,
                        item.FirstSnapshotDateUtc,
                        item.LastSnapshotDateUtc,
                        item.SnapshotsCreated,
                        item.AccruedInterestBefore,
                        item.InterestAccrued,
                        item.AccruedInterestAfter
                    },

                Metadata =
                    new
                    {
                        Module =
                            "Credit Facility Interest Accrual",

                        facility.AnnualInterestRate,

                        facility.DayCountBasis,

                        facility.OutstandingPrincipalAmount
                    }
            });
    }

    private static DateTime EarlierDate(
        DateTime first,
        DateTime second)
    {
        return first <= second
            ? first
            : second;
    }

    private static DateTime LaterDate(
        DateTime first,
        DateTime second)
    {
        return first >= second
            ? first
            : second;
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

    private static
        CreditFacilityAccrualSnapshotResponseDto Map(
            CreditFacilityInterestAccrualSnapshot snapshot)
    {
        return new CreditFacilityAccrualSnapshotResponseDto
        {
            Id =
                snapshot.Id,

            CreditFacilityId =
                snapshot.CreditFacilityId,

            SnapshotDateUtc =
                snapshot.SnapshotDateUtc,

            FacilityReference =
                snapshot.FacilityReference,

            FacilityName =
                snapshot.FacilityName,

            LenderName =
                snapshot.LenderName,

            Currency =
                snapshot.Currency,

            FacilityStatus =
                snapshot.FacilityStatus,

            OutstandingPrincipalAmount =
                snapshot.OutstandingPrincipalAmount,

            AnnualInterestRate =
                snapshot.AnnualInterestRate,

            DayCountBasis =
                snapshot.DayCountBasis,

            AccruedDays =
                snapshot.AccruedDays,

            AccruedInterestBefore =
                snapshot.AccruedInterestBefore,

            AccruedInterestAmount =
                snapshot.AccruedInterestAmount,

            AccruedInterestAfter =
                snapshot.AccruedInterestAfter,

            CreatedByUserId =
                snapshot.CreatedByUserId,

            CreatedAtUtc =
                snapshot.CreatedAtUtc
        };
    }
}