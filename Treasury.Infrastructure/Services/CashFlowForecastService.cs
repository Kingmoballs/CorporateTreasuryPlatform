using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.CashFlowForecasts;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class CashFlowForecastService
    : ICashFlowForecastService
{
    private const int MaximumForecastDays = 180;

    private static readonly HashSet<string> AllowedDirections =
    [
        CashFlowDirections.Inflow,
        CashFlowDirections.Outflow
    ];

    private static readonly HashSet<string> AllowedSourceTypes =
    [
        CashFlowForecastSourceTypes.Manual,
        CashFlowForecastSourceTypes.CustomerReceipt,
        CashFlowForecastSourceTypes.SupplierPayment,
        CashFlowForecastSourceTypes.Payroll,
        CashFlowForecastSourceTypes.Tax,
        CashFlowForecastSourceTypes.Loan,
        CashFlowForecastSourceTypes.Investment,
        CashFlowForecastSourceTypes.Other
    ];

    private readonly ICashFlowForecastRepository
        _forecastRepository;

    private readonly IAccountRepository
        _accountRepository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly ITreasuryTransactionRepository
        _transactionRepository;
    
    private readonly IAuditLogService
        _auditLogService;

    public CashFlowForecastService(
        ICashFlowForecastRepository forecastRepository,
        IAccountRepository accountRepository,
        ICurrentUserService currentUserService,
        ITreasuryTransactionRepository transactionRepository,
        IAuditLogService auditLogService)
    {
        _forecastRepository = forecastRepository;

        _accountRepository = accountRepository;

        _currentUserService = currentUserService;
        
        _transactionRepository = transactionRepository;

        _auditLogService = auditLogService;
    }

    public async Task<CashFlowForecastItemResponseDto>
        Create(
            CreateCashFlowForecastItemDto dto)
    {
        ValidateCreateDto(dto);

        var currency =
            NormalizeCurrency(dto.Currency);

        Account? account =
            null;

        if (dto.AccountId.HasValue)
        {
            account =
                await _accountRepository.GetById(
                    dto.AccountId.Value);

            if (account is null)
            {
                throw new ResourceNotFoundException(
                    "Account not found.");
            }

            if (!account.IsActive)
            {
                throw new ConflictException(
                    "Cannot create a forecast item " +
                    "for an inactive account.");
            }

            if (!string.Equals(
                account.Currency,
                currency,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException(
                    "Forecast currency must match " +
                    "the account currency.");
            }
        }

        var forecastItem =
            new CashFlowForecastItem
            {
                Id =
                    Guid.NewGuid(),

                AccountId =
                    dto.AccountId,

                Direction =
                    NormalizeDirection(
                        dto.Direction),

                Amount =
                    dto.Amount,

                Currency =
                    currency,

                ExpectedDateUtc =
                    NormalizeUtc(
                        dto.ExpectedDateUtc),

                Category =
                    dto.Category.Trim(),

                CounterpartyName =
                    NormalizeOptionalText(
                        dto.CounterpartyName),

                Description =
                    dto.Description.Trim(),

                SourceType =
                    NormalizeSourceType(
                        dto.SourceType),

                Status =
                    CashFlowForecastStatus.Active,

                CreatedByUserId =
                    _currentUserService.UserId,

                CreatedAtUtc =
                    DateTime.UtcNow,

                UpdatedAtUtc =
                    DateTime.UtcNow
            };

        await _forecastRepository.Add(
            forecastItem);

        await _forecastRepository.SaveChanges();

        var savedItem =
            await _forecastRepository.GetById(
                forecastItem.Id);

        var response =
            MapItem(savedItem ?? forecastItem);

        await RecordForecastCreatedAudit(response);

        return response;
    }

    public async Task<CashFlowForecastItemResponseDto>
        GetById(Guid id)
    {
        var forecastItem =
            await _forecastRepository.GetById(id);

        if (forecastItem is null)
        {
            throw new ResourceNotFoundException(
                "Cash flow forecast item not found.");
        }

        return MapItem(forecastItem);
    }

    public async Task<List<CashFlowForecastItemResponseDto>>
        GetActive(
            Guid? accountId,
            string? currency,
            DateTime fromUtc,
            DateTime toUtc)
    {
        var normalizedPeriod =
            NormalizeForecastPeriod(
                fromUtc,
                toUtc);

        var normalizedCurrency =
            string.IsNullOrWhiteSpace(currency)
                ? null
                : NormalizeCurrency(currency);

        var items =
            await _forecastRepository.GetActiveForPeriod(
                accountId,
                normalizedCurrency,
                normalizedPeriod.FromUtc,
                normalizedPeriod.ToUtc);

        return items
            .Select(MapItem)
            .ToList();
    }

    public async Task<CashFlowForecastItemResponseDto>
        Cancel(Guid id)
    {
        var forecastItem =
            await _forecastRepository.GetById(id);

        if (forecastItem is null)
        {
            throw new ResourceNotFoundException(
                "Cash flow forecast item not found.");
        }

        if (forecastItem.Status !=
            CashFlowForecastStatus.Active)
        {
            throw new ConflictException(
                "Only active forecast items can be cancelled.");
        }

        var beforeValues =
            SnapshotForecast(forecastItem);

        forecastItem.Status =
            CashFlowForecastStatus.Cancelled;

        forecastItem.CancelledByUserId =
            _currentUserService.UserId;

        forecastItem.CancelledAtUtc =
            DateTime.UtcNow;

        forecastItem.UpdatedAtUtc =
            DateTime.UtcNow;

        forecastItem.ConcurrencyToken =
            Guid.NewGuid();

        _forecastRepository.Update(
            forecastItem);

        await _forecastRepository.SaveChanges();

        var savedItem =
            await _forecastRepository.GetById(id);

        var response =
            MapItem(savedItem ?? forecastItem);

        await RecordForecastCancelledAudit(
            beforeValues,
            response);

        return response;
    }

    public async Task<CashFlowForecastItemResponseDto>
        Realize(
            Guid id,
            Guid treasuryTransactionId)
    {
        if (treasuryTransactionId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Treasury transaction is required.");
        }

        var forecastItem =
            await _forecastRepository.GetById(id);

        if (forecastItem is null)
        {
            throw new ResourceNotFoundException(
                "Cash flow forecast item not found.");
        }

        if (forecastItem.Status !=
            CashFlowForecastStatus.Active)
        {
            throw new ConflictException(
                "Only active forecast items can be realized.");
        }

        var transaction =
            await _transactionRepository.GetById(
                treasuryTransactionId);

        if (transaction is null)
        {
            throw new ResourceNotFoundException(
                "Treasury transaction not found.");
        }

        EnsureTransactionCanRealizeForecast(
            forecastItem,
            transaction);

        var alreadyRealized =
            await _forecastRepository
                .TreasuryTransactionAlreadyRealized(
                    treasuryTransactionId,
                    forecastItem.Id);

        if (alreadyRealized)
        {
            throw new ConflictException(
                "This treasury transaction has already " +
                "been linked to another forecast item.");
        }

        var beforeValues =
            SnapshotForecast(forecastItem);

        forecastItem.Status =
            CashFlowForecastStatus.Realized;

        forecastItem.RealizedTreasuryTransactionId =
            transaction.Id;

        forecastItem.RealizedAtUtc =
            DateTime.UtcNow;

        forecastItem.UpdatedAtUtc =
            DateTime.UtcNow;

        forecastItem.ConcurrencyToken =
            Guid.NewGuid();

        _forecastRepository.Update(
            forecastItem);

        await _forecastRepository.SaveChanges();

        var savedItem =
            await _forecastRepository.GetById(id);

        var response =
            MapItem(savedItem ?? forecastItem);

        await RecordForecastRealizedAudit(
            beforeValues,
            response);

        return response;
    }

    public async Task<CashFlowForecastReportDto>
        GetForecastReport(
            Guid? accountId,
            string? currency,
            DateTime fromUtc,
            DateTime toUtc,
            decimal minimumLiquidityThreshold)
    {
        if (minimumLiquidityThreshold < 0)
        {
            throw new BusinessRuleException(
                "Minimum liquidity threshold cannot be negative.");
        }

        var normalizedPeriod =
            NormalizeForecastPeriod(
                fromUtc,
                toUtc);

        Account? account =
            null;

        string normalizedCurrency;

        decimal openingAvailableBalance;

        if (accountId.HasValue)
        {
            account =
                await _accountRepository.GetById(
                    accountId.Value);

            if (account is null)
            {
                throw new ResourceNotFoundException(
                    "Account not found.");
            }

            normalizedCurrency =
                account.Currency.Trim().ToUpperInvariant();

            openingAvailableBalance =
                account.AvailableBalance;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(currency))
            {
                throw new BusinessRuleException(
                    "Currency is required when no account is selected.");
            }

            normalizedCurrency =
                NormalizeCurrency(currency);

            var accounts =
                await _accountRepository.GetAll();

            openingAvailableBalance =
                accounts
                    .Where(accountItem =>
                        accountItem.IsActive &&
                        string.Equals(
                            accountItem.Currency,
                            normalizedCurrency,
                            StringComparison.OrdinalIgnoreCase))
                    .Sum(accountItem =>
                        accountItem.AvailableBalance);
        }

        var items =
            await _forecastRepository.GetActiveForPeriod(
                accountId,
                normalizedCurrency,
                normalizedPeriod.FromUtc,
                normalizedPeriod.ToUtc);

        var dailyForecasts =
            BuildDailyForecasts(
                items,
                normalizedPeriod.FromUtc,
                normalizedPeriod.ToUtc,
                openingAvailableBalance,
                minimumLiquidityThreshold);

        return new CashFlowForecastReportDto
        {
            GeneratedAtUtc =
                DateTime.UtcNow,

            AccountId =
                accountId,

            AccountName =
                account?.Name,

            Currency =
                normalizedCurrency,

            FromUtc =
                normalizedPeriod.FromUtc,

            ToUtc =
                normalizedPeriod.ToUtc,

            OpeningAvailableBalance =
                openingAvailableBalance,

            TotalExpectedInflow =
                dailyForecasts.Sum(day =>
                    day.ExpectedInflow),

            TotalExpectedOutflow =
                dailyForecasts.Sum(day =>
                    day.ExpectedOutflow),

            NetMovement =
                dailyForecasts.Sum(day =>
                    day.NetMovement),

            ProjectedClosingBalance =
                dailyForecasts.Count == 0
                    ? openingAvailableBalance
                    : dailyForecasts.Last().ClosingBalance,

            MinimumProjectedBalance =
                dailyForecasts.Count == 0
                    ? openingAvailableBalance
                    : dailyForecasts.Min(day =>
                        day.ClosingBalance),

            MinimumLiquidityThreshold =
                minimumLiquidityThreshold,

            LiquidityGapDayCount =
                dailyForecasts.Count(day =>
                    day.IsLiquidityGap),

            DailyForecasts =
                dailyForecasts
        };
    }

    private static List<CashFlowForecastDailyBucketDto>
        BuildDailyForecasts(
            List<CashFlowForecastItem> items,
            DateTime fromUtc,
            DateTime toUtc,
            decimal openingAvailableBalance,
            decimal minimumLiquidityThreshold)
    {
        var itemsByDate =
            items
                .GroupBy(item =>
                    item.ExpectedDateUtc.Date)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(item =>
                            item.ExpectedDateUtc)
                        .ToList());

        var dailyForecasts =
            new List<CashFlowForecastDailyBucketDto>();

        var runningBalance =
            openingAvailableBalance;

        var currentDate =
            fromUtc.Date;

        var finalDate =
            toUtc.Date;

        while (currentDate <= finalDate)
        {
            var dayItems =
                itemsByDate.TryGetValue(
                    currentDate,
                    out var groupedItems)
                    ? groupedItems
                    : new List<CashFlowForecastItem>();

            var expectedInflow =
                dayItems
                    .Where(item =>
                        item.Direction ==
                        CashFlowDirections.Inflow)
                    .Sum(item =>
                        item.Amount);

            var expectedOutflow =
                dayItems
                    .Where(item =>
                        item.Direction ==
                        CashFlowDirections.Outflow)
                    .Sum(item =>
                        item.Amount);

            var openingBalance =
                runningBalance;

            var netMovement =
                expectedInflow - expectedOutflow;

            var closingBalance =
                openingBalance + netMovement;

            var liquidityGapAmount =
                Math.Max(
                    0m,
                    minimumLiquidityThreshold -
                    closingBalance);

            dailyForecasts.Add(
                new CashFlowForecastDailyBucketDto
                {
                    DateUtc =
                        currentDate,

                    OpeningBalance =
                        openingBalance,

                    ExpectedInflow =
                        expectedInflow,

                    ExpectedOutflow =
                        expectedOutflow,

                    NetMovement =
                        netMovement,

                    ClosingBalance =
                        closingBalance,

                    IsLiquidityGap =
                        liquidityGapAmount > 0,

                    LiquidityGapAmount =
                        liquidityGapAmount,

                    Items =
                        dayItems
                            .Select(MapItem)
                            .ToList()
                });

            runningBalance =
                closingBalance;

            currentDate =
                currentDate.AddDays(1);
        }

        return dailyForecasts;
    }

    private static void ValidateCreateDto(
        CreateCashFlowForecastItemDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Direction))
        {
            throw new BusinessRuleException(
                "Forecast direction is required.");
        }

        if (dto.Amount <= 0)
        {
            throw new BusinessRuleException(
                "Forecast amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(dto.Currency))
        {
            throw new BusinessRuleException(
                "Currency is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Category))
        {
            throw new BusinessRuleException(
                "Forecast category is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Description))
        {
            throw new BusinessRuleException(
                "Forecast description is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.SourceType))
        {
            throw new BusinessRuleException(
                "Forecast source type is required.");
        }
    }

    private static void EnsureTransactionCanRealizeForecast(
        CashFlowForecastItem forecastItem,
        TreasuryTransaction transaction)
    {
        if (transaction.Status !=
            TransactionStatuses.Completed)
        {
            throw new ConflictException(
                "Only completed treasury transactions " +
                "can realize a forecast item.");
        }

        if (!string.Equals(
            transaction.Currency,
            forecastItem.Currency,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "The treasury transaction currency does " +
                "not match the forecast currency.");
        }

        if (transaction.Amount != forecastItem.Amount)
        {
            throw new BusinessRuleException(
                "The treasury transaction amount does " +
                "not match the forecast amount.");
        }

        /*
        * Account-specific forecasts must match the exact
        * account side of the treasury transaction.
        */
        if (forecastItem.AccountId.HasValue)
        {
            if (forecastItem.Direction ==
                CashFlowDirections.Inflow &&
                transaction.DestinationAccountId !=
                    forecastItem.AccountId.Value)
            {
                throw new BusinessRuleException(
                    "The treasury transaction does not " +
                    "represent cash coming into the " +
                    "forecast account.");
            }

            if (forecastItem.Direction ==
                CashFlowDirections.Outflow &&
                transaction.SourceAccountId !=
                    forecastItem.AccountId.Value)
            {
                throw new BusinessRuleException(
                    "The treasury transaction does not " +
                    "represent cash leaving the " +
                    "forecast account.");
            }

            return;
        }

        /*
        * Consolidated currency forecasts should not be realized
        * with internal transfers because internal transfers do not
        * change total cash for that currency.
        */
        if (transaction.TransactionType ==
            TransactionTypes.InternalTransfer)
        {
            throw new BusinessRuleException(
                "A consolidated forecast item cannot be " +
                "realized by an internal transfer. Use an " +
                "account-specific forecast item instead.");
        }

        if (forecastItem.Direction ==
            CashFlowDirections.Inflow &&
            transaction.DestinationAccountId is null)
        {
            throw new BusinessRuleException(
                "The treasury transaction does not represent " +
                "a cash inflow.");
        }

        if (forecastItem.Direction ==
            CashFlowDirections.Outflow &&
            transaction.SourceAccountId is null)
        {
            throw new BusinessRuleException(
                "The treasury transaction does not represent " +
                "a cash outflow.");
        }
    }

    private static ForecastPeriod NormalizeForecastPeriod(
        DateTime fromUtc,
        DateTime toUtc)
    {
        var normalizedFrom =
            NormalizeUtc(fromUtc).Date;

        var normalizedToDate =
            NormalizeUtc(toUtc).Date;

        if (normalizedFrom > normalizedToDate)
        {
            throw new BusinessRuleException(
                "Forecast start date cannot be later " +
                "than forecast end date.");
        }

        if ((normalizedToDate - normalizedFrom).TotalDays >
            MaximumForecastDays)
        {
            throw new BusinessRuleException(
                $"Forecast period cannot exceed " +
                $"{MaximumForecastDays} days.");
        }

        var inclusiveToUtc =
            normalizedToDate
                .AddDays(1)
                .AddTicks(-1);

        return new ForecastPeriod(
            normalizedFrom,
            inclusiveToUtc);
    }

    private static string NormalizeDirection(
        string direction)
    {
        var normalized =
            direction.Trim();

        var match =
            AllowedDirections.FirstOrDefault(value =>
                string.Equals(
                    value,
                    normalized,
                    StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new BusinessRuleException(
                "Forecast direction must be either " +
                "Inflow or Outflow.");
        }

        return match;
    }

    private static string NormalizeSourceType(
        string sourceType)
    {
        var normalized =
            sourceType.Trim();

        var match =
            AllowedSourceTypes.FirstOrDefault(value =>
                string.Equals(
                    value,
                    normalized,
                    StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new BusinessRuleException(
                "Invalid forecast source type.");
        }

        return match;
    }

    private static string NormalizeCurrency(
        string currency)
    {
        var normalized =
            currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3)
        {
            throw new BusinessRuleException(
                "Currency must be a 3-letter code.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static DateTime NormalizeUtc(
        DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Local)
        {
            return value.ToUniversalTime();
        }

        return DateTime.SpecifyKind(
            value,
            DateTimeKind.Utc);
    }

    private static CashFlowForecastItemResponseDto MapItem(
        CashFlowForecastItem item)
    {
        return new CashFlowForecastItemResponseDto
        {
            Id =
                item.Id,

            AccountId =
                item.AccountId,

            AccountName =
                item.Account?.Name,

            Direction =
                item.Direction,

            Amount =
                item.Amount,

            Currency =
                item.Currency,

            ExpectedDateUtc =
                item.ExpectedDateUtc,

            Category =
                item.Category,

            CounterpartyName =
                item.CounterpartyName,

            Description =
                item.Description,

            SourceType =
                item.SourceType,

            Status =
                item.Status,

            CreatedByUserId =
                item.CreatedByUserId,

            CreatedAtUtc =
                item.CreatedAtUtc,

            UpdatedAtUtc =
                item.UpdatedAtUtc,

            CancelledByUserId =
                item.CancelledByUserId,

            CancelledAtUtc =
                item.CancelledAtUtc,

            RealizedTreasuryTransactionId =
                item.RealizedTreasuryTransactionId,

            RealizedAtUtc =
                item.RealizedAtUtc
        };
    }

    private async Task RecordForecastCreatedAudit(
        CashFlowForecastItemResponseDto item)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Created,

                EntityType =
                    AuditEntityTypes.CashFlowForecastItem,

                EntityId =
                    item.Id,

                EntityReference =
                    item.Id.ToString(),

                Summary =
                    $"Cash flow forecast item {item.Direction} " +
                    $"{item.Amount:N2} {item.Currency} was created.",

                AfterValues =
                    SnapshotForecast(item),

                Metadata =
                    new
                    {
                        Module = "Cash Flow Forecasts"
                    }
            });
    }

    private async Task RecordForecastCancelledAudit(
        object beforeValues,
        CashFlowForecastItemResponseDto item)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Cancelled,

                EntityType =
                    AuditEntityTypes.CashFlowForecastItem,

                EntityId =
                    item.Id,

                EntityReference =
                    item.Id.ToString(),

                Summary =
                    $"Cash flow forecast item {item.Id} was cancelled.",

                BeforeValues =
                    beforeValues,

                AfterValues =
                    SnapshotForecast(item),

                Metadata =
                    new
                    {
                        Module = "Cash Flow Forecasts"
                    }
            });
    }

    private async Task RecordForecastRealizedAudit(
        object beforeValues,
        CashFlowForecastItemResponseDto item)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Realized,

                EntityType =
                    AuditEntityTypes.CashFlowForecastItem,

                EntityId =
                    item.Id,

                EntityReference =
                    item.Id.ToString(),

                Summary =
                    $"Cash flow forecast item {item.Id} was realized.",

                BeforeValues =
                    beforeValues,

                AfterValues =
                    SnapshotForecast(item),

                Metadata =
                    new
                    {
                        Module = "Cash Flow Forecasts",
                        item.RealizedTreasuryTransactionId
                    }
            });
    }

    private static object SnapshotForecast(
        CashFlowForecastItem item)
    {
        return new
        {
            item.Id,
            item.AccountId,
            item.Direction,
            item.Amount,
            item.Currency,
            item.ExpectedDateUtc,
            item.Category,
            item.CounterpartyName,
            item.Description,
            item.SourceType,
            item.Status,
            item.CreatedByUserId,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            item.CancelledByUserId,
            item.CancelledAtUtc,
            item.RealizedTreasuryTransactionId,
            item.RealizedAtUtc
        };
    }

    private static object SnapshotForecast(
        CashFlowForecastItemResponseDto item)
    {
        return new
        {
            item.Id,
            item.AccountId,
            item.AccountName,
            item.Direction,
            item.Amount,
            item.Currency,
            item.ExpectedDateUtc,
            item.Category,
            item.CounterpartyName,
            item.Description,
            item.SourceType,
            item.Status,
            item.CreatedByUserId,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            item.CancelledByUserId,
            item.CancelledAtUtc,
            item.RealizedTreasuryTransactionId,
            item.RealizedAtUtc
        };
    }

    private sealed record ForecastPeriod(
        DateTime FromUtc,
        DateTime ToUtc);
}