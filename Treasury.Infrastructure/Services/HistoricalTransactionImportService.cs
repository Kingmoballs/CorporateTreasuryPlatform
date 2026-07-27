using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.Exports;
using Treasury.Application.DTOs.HistoricalImports;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Common;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class HistoricalTransactionImportService
    : IHistoricalTransactionImportService
{
    private static readonly JsonSerializerOptions
        JsonOptions = new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase
        };

    private static readonly string[]
        HistoricalTransactionHeaders =
        {
            "ExternalReference",
            "AccountNumber",
            "LegalEntityCode",
            "BusinessUnitCode",
            "TransactionDateUtc",
            "ValueDateUtc",
            "Amount",
            "Currency",
            "Direction",
            "TransactionType",
            "Description",
            "Category",
            "CounterpartyName"
        };

    private static readonly string[]
        CutoverOpeningBalanceHeaders =
        {
            "ExternalReference",
            "AccountNumber",
            "LegalEntityCode",
            "BusinessUnitCode",
            "CutoverDateUtc",
            "OpeningBalance",
            "Currency",
            "Description"
        };

    private readonly
        IHistoricalTransactionImportRepository
        _repository;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuditLogService
        _auditLogService;

    private readonly HistoricalImportOptions _options;

    private readonly TimeProvider _timeProvider;

    public HistoricalTransactionImportService(
        IHistoricalTransactionImportRepository
            repository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IOptions<HistoricalImportOptions> options,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public HistoricalImportTemplateDto GetTemplate(
        string mode)
    {
        var normalizedMode = NormalizeMode(mode);
        var headers = GetHeaders(normalizedMode);
        var csv = string.Join(",", headers) +
            Environment.NewLine;

        return new HistoricalImportTemplateDto
        {
            Mode = normalizedMode,
            FileName = normalizedMode ==
                HistoricalImportModes
                    .HistoricalTransactions
                ? "historical-transactions-template.csv"
                : "cutover-opening-balances-template.csv",
            ContentType = "text/csv; charset=utf-8",
            Content =
                CsvExportHelper.ToUtf8Bytes(csv)
        };
    }

    public async Task<
        HistoricalImportBatchResponseDto> DryRun(
            CreateHistoricalImportDryRunDto dto)
    {
        ValidateUpload(dto);

        var mode = NormalizeMode(dto.Mode);
        var fileHash = Convert.ToHexString(
            SHA256.HashData(dto.FileContent));

        var existingByKey =
            await _repository.GetByImportKey(
                dto.ImportKey);

        if (existingByKey is not null)
        {
            if (existingByKey.Mode != mode ||
                !string.Equals(
                    existingByKey.FileHash,
                    fileHash,
                    StringComparison.Ordinal))
            {
                throw new ConflictException(
                    "This Idempotency-Key has already " +
                    "been used for a different historical " +
                    "import payload.");
            }

            return MapBatch(
                existingByKey,
                isIdempotentReplay: true);
        }

        var existingByHash =
            await _repository.GetByFileHash(
                mode,
                fileHash);

        if (existingByHash is not null)
        {
            throw new ConflictException(
                "This exact file has already been " +
                $"uploaded as batch " +
                $"'{existingByHash.Id}'. Reuse its " +
                "original Idempotency-Key to replay " +
                "the response.");
        }

        var parsed = ParseCsv(
            dto.FileContent,
            GetHeaders(mode));

        if (parsed.Rows.Count == 0)
        {
            throw new RequestValidationException(
                "The CSV file must contain at least " +
                "one data row.");
        }

        if (parsed.Rows.Count >
            _options.MaximumRowCount)
        {
            throw new RequestValidationException(
                $"The CSV file contains " +
                $"{parsed.Rows.Count} rows; the " +
                $"configured maximum is " +
                $"{_options.MaximumRowCount}.");
        }

        var accountNumbers = parsed.Rows
            .Select(row =>
                GetValue(
                    row.Values,
                    "AccountNumber"))
            .Where(value =>
                !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var accounts =
            await _repository.GetAccountsByNumbers(
                accountNumbers);

        var now =
            _timeProvider.GetUtcNow().UtcDateTime;

        var batch =
            new HistoricalTransactionImportBatch
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    RequireOrganizationId(),
                ImportKey = dto.ImportKey,
                Mode = mode,
                FileName =
                    NormalizeFileName(dto.FileName),
                FileHash = fileHash,
                UploadedByUserId =
                    _currentUserService.UserId,
                UploadedAtUtc = now,
                ValidatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            };

        foreach (var parsedRow in parsed.Rows)
        {
            batch.Rows.Add(
                CreateAndValidateRow(
                    batch,
                    parsedRow,
                    accounts,
                    mode,
                    now));
        }

        var resolvedAccountIds = batch.Rows
            .Where(row => row.AccountId.HasValue)
            .Select(row => row.AccountId!.Value)
            .Distinct()
            .ToArray();

        var accountsWithActivity =
            mode == HistoricalImportModes
                .CutoverOpeningBalances
                ? await _repository
                    .GetAccountIdsWithFinancialActivity(
                        resolvedAccountIds)
                : new HashSet<Guid>();

        ApplyCutoverActivityValidation(
            batch.Rows,
            mode,
            accountsWithActivity);

        ApplyWithinBatchDuplicateValidation(
            batch.Rows);

        var candidateFingerprints =
            batch.Rows
                .Where(row => row.IsValid)
                .Select(row => row.Fingerprint)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        var priorFingerprints =
            await _repository
                .GetFingerprintsInValidatedBatches(
                    mode,
                    candidateFingerprints);

        ApplyPriorDuplicateValidation(
            batch.Rows,
            priorFingerprints);

        batch.TotalRowCount = batch.Rows.Count;
        batch.ValidRowCount =
            batch.Rows.Count(row => row.IsValid);
        batch.InvalidRowCount =
            batch.TotalRowCount -
            batch.ValidRowCount;
        batch.Status =
            batch.InvalidRowCount == 0
                ? HistoricalImportStatuses.Validated
                : HistoricalImportStatuses
                    .ValidationFailed;

        try
        {
            await _repository.Add(batch);
            await _repository.SaveChanges();
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The import collided with another " +
                "request using the same idempotency key " +
                "or file. Retrieve the existing batch " +
                "before retrying.");
        }

        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action = AuditActionTypes.Imported,
                EntityType =
                    AuditEntityTypes
                        .HistoricalTransactionImportBatch,
                EntityId = batch.Id,
                EntityReference = batch.FileName,
                Summary =
                    "Historical financial data CSV was " +
                    "staged and validated without posting.",
                AfterValues = new
                {
                    batch.Mode,
                    batch.Status,
                    batch.TotalRowCount,
                    batch.ValidRowCount,
                    batch.InvalidRowCount,
                    IsPostingOperation = false
                },
                Metadata = new
                {
                    batch.ImportKey,
                    batch.FileHash
                }
            });

        return MapBatch(
            batch,
            isIdempotentReplay: false);
    }

    public async Task<
        HistoricalImportBatchResponseDto> GetBatch(
            Guid batchId)
    {
        var batch =
            await RequireBatch(batchId);

        return MapBatch(
            batch,
            isIdempotentReplay: false);
    }

    public async Task<
        PagedHistoricalImportRowsResponseDto> GetRows(
            Guid batchId,
            HistoricalImportRowsQueryDto query)
    {
        await RequireBatch(batchId);

        query.Page = Math.Max(1, query.Page);
        query.PageSize =
            Math.Clamp(query.PageSize, 1, 200);

        var result =
            await _repository.GetRows(
                batchId,
                query);

        return new
            PagedHistoricalImportRowsResponseDto
            {
                Items = result.Items
                    .Select(MapRow)
                    .ToArray(),
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = result.TotalCount,
                TotalPages = (int)Math.Ceiling(
                    result.TotalCount /
                    (double)query.PageSize)
            };
    }

    public async Task<
        HistoricalImportBatchResponseDto> Submit(
            Guid batchId,
            HistoricalImportConcurrencyDto dto)
    {
        ValidateConcurrencyToken(
            dto.ConcurrencyToken);

        await _repository.BeginTransaction();

        try
        {
            var batch =
                await RequireBatchForUpdate(batchId);

            if (batch.Status !=
                HistoricalImportStatuses.Validated)
            {
                throw new ConflictException(
                    "Only a successfully validated " +
                    "batch can be submitted for approval.");
            }

            if (batch.InvalidRowCount != 0 ||
                batch.ValidRowCount == 0)
            {
                throw new ConflictException(
                    "The batch must contain valid rows " +
                    "and no validation errors.");
            }

            _repository.SetOriginalConcurrencyToken(
                batch,
                dto.ConcurrencyToken);

            var now =
                _timeProvider.GetUtcNow()
                    .UtcDateTime;

            batch.Status =
                HistoricalImportStatuses
                    .PendingApproval;
            batch.SubmittedByUserId =
                _currentUserService.UserId;
            batch.SubmittedAtUtc = now;
            batch.RequiredApprovalCount =
                batch.Mode ==
                HistoricalImportModes
                    .CutoverOpeningBalances
                    ? 2
                    : 1;
            batch.ApprovalCount = 0;
            batch.ApprovedAtUtc = null;
            batch.RejectedByUserId = null;
            batch.RejectedAtUtc = null;
            batch.RejectionReason = null;
            batch.ConcurrencyToken =
                Guid.NewGuid();

            await _repository.SaveChanges();

            await RecordBatchAudit(
                batch,
                AuditActionTypes.Updated,
                "Historical import batch submitted " +
                "for independent approval.");

            await _repository.CommitTransaction();

            return MapBatch(
                batch,
                isIdempotentReplay: false);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _repository.RollbackTransaction();

            throw new ConflictException(
                "The historical import batch changed " +
                "before it could be submitted.");
        }
        catch
        {
            await _repository.RollbackTransaction();
            throw;
        }
    }

    public async Task<
        HistoricalImportBatchResponseDto> Approve(
            Guid batchId,
            ReviewHistoricalImportDto dto)
    {
        ValidateConcurrencyToken(
            dto.ConcurrencyToken);
        ValidateOptionalComment(dto.Comment);

        await _repository.BeginTransaction();

        try
        {
            var batch =
                await RequireBatchForUpdate(batchId);

            EnsurePendingApproval(batch);
            EnsureReviewerIsIndependent(batch);

            var role = NormalizeApproverRole(
                batch.Mode,
                _currentUserService.Role);

            if (await _repository.HasDecision(
                    batch.Id,
                    _currentUserService.UserId))
            {
                throw new ConflictException(
                    "You have already reviewed this " +
                    "historical import batch.");
            }

            if (batch.Mode ==
                    HistoricalImportModes
                        .CutoverOpeningBalances &&
                batch.Decisions.Any(decision =>
                    decision.Decision ==
                        ApprovalDecisionTypes
                            .Approved &&
                    decision.ApproverRole == role))
            {
                throw new ConflictException(
                    $"A {role} approval has already " +
                    "been recorded. Cutover requires " +
                    "one Admin and one CFO approval.");
            }

            _repository.SetOriginalConcurrencyToken(
                batch,
                dto.ConcurrencyToken);

            var now =
                _timeProvider.GetUtcNow()
                    .UtcDateTime;

            var decision =
                new HistoricalTransactionImportDecision
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        batch.OrganizationId,
                    BatchId = batch.Id,
                    ApproverUserId =
                        _currentUserService.UserId,
                    ApproverRole = role,
                    Decision =
                        ApprovalDecisionTypes
                            .Approved,
                    Comment =
                        NormalizeOptional(
                            dto.Comment),
                    CreatedAtUtc = now
                };

            var approvedDecisions =
                batch.Decisions
                    .Where(item =>
                        item.Decision ==
                            ApprovalDecisionTypes
                                .Approved)
                    .Append(decision)
                    .ToArray();

            await _repository.AddDecision(decision);

            batch.ApprovalCount =
                batch.Mode ==
                HistoricalImportModes
                    .CutoverOpeningBalances
                    ? approvedDecisions
                        .Select(item =>
                            item.ApproverRole)
                        .Distinct(
                            StringComparer
                                .OrdinalIgnoreCase)
                        .Count()
                    : approvedDecisions.Length;

            var fullyApproved =
                HasRequiredApprovals(
                    batch,
                    approvedDecisions);

            if (fullyApproved)
            {
                batch.Status =
                    HistoricalImportStatuses.Approved;
                batch.ApprovedAtUtc = now;
            }

            batch.ConcurrencyToken =
                Guid.NewGuid();

            await _repository.SaveChanges();

            await RecordBatchAudit(
                batch,
                AuditActionTypes.Approved,
                fullyApproved
                    ? "Historical import batch received " +
                      "all required approvals."
                    : "Historical import batch received " +
                      "a partial approval.");

            await _repository.CommitTransaction();

            return MapBatch(
                batch,
                isIdempotentReplay: false);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _repository.RollbackTransaction();

            throw new ConflictException(
                "The historical import batch changed " +
                "while approval was processing.");
        }
        catch
        {
            await _repository.RollbackTransaction();
            throw;
        }
    }

    public async Task<
        HistoricalImportBatchResponseDto> Reject(
            Guid batchId,
            RejectHistoricalImportDto dto)
    {
        ValidateConcurrencyToken(
            dto.ConcurrencyToken);

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new RequestValidationException(
                "A rejection reason is required.");
        }

        if (dto.Reason.Trim().Length > 500)
        {
            throw new RequestValidationException(
                "The rejection reason cannot exceed " +
                "500 characters.");
        }

        await _repository.BeginTransaction();

        try
        {
            var batch =
                await RequireBatchForUpdate(batchId);

            EnsurePendingApproval(batch);
            EnsureReviewerIsIndependent(batch);

            var role = NormalizeApproverRole(
                batch.Mode,
                _currentUserService.Role);

            if (await _repository.HasDecision(
                    batch.Id,
                    _currentUserService.UserId))
            {
                throw new ConflictException(
                    "You have already reviewed this " +
                    "historical import batch.");
            }

            _repository.SetOriginalConcurrencyToken(
                batch,
                dto.ConcurrencyToken);

            var now =
                _timeProvider.GetUtcNow()
                    .UtcDateTime;
            var reason = dto.Reason.Trim();

            await _repository.AddDecision(
                new HistoricalTransactionImportDecision
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        batch.OrganizationId,
                    BatchId = batch.Id,
                    ApproverUserId =
                        _currentUserService.UserId,
                    ApproverRole = role,
                    Decision =
                        ApprovalDecisionTypes
                            .Rejected,
                    Comment = reason,
                    CreatedAtUtc = now
                });

            batch.Status =
                HistoricalImportStatuses.Rejected;
            batch.RejectedByUserId =
                _currentUserService.UserId;
            batch.RejectedAtUtc = now;
            batch.RejectionReason = reason;
            batch.ConcurrencyToken =
                Guid.NewGuid();

            await _repository.SaveChanges();

            await RecordBatchAudit(
                batch,
                AuditActionTypes.Rejected,
                "Historical import batch was rejected.");

            await _repository.CommitTransaction();

            return MapBatch(
                batch,
                isIdempotentReplay: false);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _repository.RollbackTransaction();

            throw new ConflictException(
                "The historical import batch changed " +
                "while rejection was processing.");
        }
        catch
        {
            await _repository.RollbackTransaction();
            throw;
        }
    }

    public async Task<IReadOnlyList<
        HistoricalImportDecisionResponseDto>>
        GetDecisions(Guid batchId)
    {
        await RequireBatch(batchId);

        var decisions =
            await _repository.GetDecisions(batchId);

        return decisions
            .Select(MapDecision)
            .ToArray();
    }

    public async Task<
        HistoricalImportCommitResponseDto> Commit(
            Guid batchId,
            HistoricalImportConcurrencyDto dto)
    {
        ValidateConcurrencyToken(
            dto.ConcurrencyToken);

        if (!string.Equals(
                _currentUserService.Role,
                Roles.Admin,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenOperationException(
                "Only an organization Admin can commit " +
                "an approved historical import batch.");
        }

        await _repository.BeginTransaction();

        try
        {
            var batch =
                await RequireBatchForUpdate(batchId);

            if (batch.Status !=
                HistoricalImportStatuses.Approved)
            {
                throw new ConflictException(
                    "Only a fully approved batch can be " +
                    "committed.");
            }

            if (!HasRequiredApprovals(
                    batch,
                    batch.Decisions.Where(item =>
                        item.Decision ==
                            ApprovalDecisionTypes
                                .Approved)))
            {
                throw new ConflictException(
                    "The required independent approvals " +
                    "are incomplete.");
            }

            if (batch.Rows.Count == 0 ||
                batch.Rows.Any(row => !row.IsValid))
            {
                throw new ConflictException(
                    "Only a batch containing validated " +
                    "rows can be committed.");
            }

            _repository.SetOriginalConcurrencyToken(
                batch,
                dto.ConcurrencyToken);

            var priorFingerprints =
                await _repository
                    .GetFingerprintsInValidatedBatches(
                        batch.Mode,
                        batch.Rows
                            .Select(row =>
                                row.Fingerprint)
                            .ToArray(),
                        batch.Id);

            if (priorFingerprints.Count > 0)
            {
                throw new ConflictException(
                    "One or more rows now duplicate " +
                    "another active or committed batch. " +
                    "The batch cannot be committed.");
            }

            var accountIds =
                batch.Rows
                    .Select(row =>
                        row.AccountId ??
                        throw new ConflictException(
                            $"Row {row.RowNumber} no " +
                            "longer resolves to an account."))
                    .Distinct()
                    .ToArray();

            var accounts =
                await _repository
                    .GetAccountsForUpdate(
                        accountIds);

            if (accounts.Count != accountIds.Length)
            {
                throw new ConflictException(
                    "One or more imported accounts no " +
                    "longer exist in this organization.");
            }

            var now =
                _timeProvider.GetUtcNow()
                    .UtcDateTime;
            batch.CommittedByUserId =
                _currentUserService.UserId;
            batch.CommittedAtUtc = now;
            var historicalRecords =
                new List<
                    HistoricalTransactionRecord>();
            var transactions =
                new List<TreasuryTransaction>();
            var ledgerEntries =
                new List<LedgerEntry>();

            if (batch.Mode ==
                HistoricalImportModes
                    .HistoricalTransactions)
            {
                CreateHistoricalRecords(
                    batch,
                    accounts,
                    now,
                    historicalRecords);

                await _repository
                    .AddHistoricalRecords(
                        historicalRecords);
            }
            else
            {
                await CreateOpeningBalancePostings(
                    batch,
                    accounts,
                    now,
                    transactions,
                    ledgerEntries);

                await _repository
                    .AddTreasuryTransactions(
                        transactions);
                await _repository.AddLedgerEntries(
                    ledgerEntries);
            }

            batch.Status =
                HistoricalImportStatuses.Committed;
            batch.ConcurrencyToken =
                Guid.NewGuid();

            await _repository.SaveChanges();

            await RecordBatchAudit(
                batch,
                AuditActionTypes.Imported,
                batch.Mode ==
                    HistoricalImportModes
                        .HistoricalTransactions
                    ? "Approved historical transactions " +
                      "were committed as immutable " +
                      "reporting records without changing " +
                      "live balances."
                    : "Approved cutover opening balances " +
                      "were posted atomically.");

            await _repository.CommitTransaction();

            return new
                HistoricalImportCommitResponseDto
                {
                    Batch = MapBatch(
                        batch,
                        isIdempotentReplay: false),
                    HistoricalRecordCount =
                        historicalRecords.Count,
                    OpeningBalancePostingCount =
                        transactions.Count,
                    TreasuryTransactionIds =
                        transactions
                            .Select(item => item.Id)
                            .ToArray()
                };
        }
        catch (DbUpdateConcurrencyException)
        {
            await _repository.RollbackTransaction();

            throw new ConflictException(
                "The batch or an account changed while " +
                "the import was being committed.");
        }
        catch
        {
            await _repository.RollbackTransaction();
            throw;
        }
    }

    public async Task<
        PagedHistoricalTransactionRecordsResponseDto>
        GetCommittedRecords(
            HistoricalTransactionRecordQueryDto query)
    {
        if (query.FromUtc.HasValue &&
            query.ToUtc.HasValue &&
            query.FromUtc.Value >= query.ToUtc.Value)
        {
            throw new RequestValidationException(
                "FromUtc must be earlier than ToUtc.");
        }

        query.Page = Math.Max(1, query.Page);
        query.PageSize =
            Math.Clamp(query.PageSize, 1, 200);

        var result =
            await _repository
                .GetCommittedRecords(query);

        return new
            PagedHistoricalTransactionRecordsResponseDto
            {
                Items = result.Items
                    .Select(MapHistoricalRecord)
                    .ToArray(),
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = result.TotalCount,
                TotalPages = (int)Math.Ceiling(
                    result.TotalCount /
                    (double)query.PageSize)
            };
    }

    public async Task<CsvExportDto> ExportErrors(
        Guid batchId)
    {
        var batch = await RequireBatch(batchId);

        var query =
            new HistoricalImportRowsQueryDto
            {
                Page = 1,
                PageSize = _options.MaximumRowCount,
                IsValid = false
            };

        var result =
            await _repository.GetRows(
                batchId,
                query);

        var builder = new StringBuilder();
        builder.AppendLine(
            "RowNumber,ExternalReference," +
            "AccountNumber,ValidationErrors");

        foreach (var row in result.Items)
        {
            var errors =
                DeserializeErrors(
                    row.ValidationErrorsJson);

            builder.Append(
                CsvExportHelper.Escape(
                    row.RowNumber));
            builder.Append(',');
            builder.Append(
                CsvExportHelper.Escape(
                    NeutralizeSpreadsheetFormula(
                        row.ExternalReference)));
            builder.Append(',');
            builder.Append(
                CsvExportHelper.Escape(
                    NeutralizeSpreadsheetFormula(
                        row.AccountNumber)));
            builder.Append(',');
            builder.AppendLine(
                CsvExportHelper.Escape(
                    string.Join(" | ", errors)));
        }

        return new CsvExportDto
        {
            FileName =
                $"historical-import-{batch.Id}-errors.csv",
            ContentType = "text/csv; charset=utf-8",
            Content = CsvExportHelper.ToUtf8Bytes(
                builder.ToString())
        };
    }

    private HistoricalTransactionImportRow
        CreateAndValidateRow(
            HistoricalTransactionImportBatch batch,
            ParsedCsvRow parsedRow,
            IReadOnlyDictionary<string, Account>
                accounts,
            string mode,
            DateTime now)
    {
        var errors = new List<string>();
        var values = parsedRow.Values;

        if (parsedRow.ColumnCountMismatch)
        {
            errors.Add(
                $"Row has {parsedRow.ActualColumnCount} " +
                $"columns; expected " +
                $"{parsedRow.ExpectedColumnCount}.");
        }

        var accountNumber =
            NormalizeOptional(
                GetValue(values, "AccountNumber"));

        var externalReference =
            NormalizeOptional(
                GetValue(
                    values,
                    "ExternalReference"));

        var legalEntityCode =
            NormalizeOptional(
                GetValue(
                    values,
                    "LegalEntityCode"));

        var businessUnitCode =
            NormalizeOptional(
                GetValue(
                    values,
                    "BusinessUnitCode"));

        var currency =
            NormalizeOptional(
                GetValue(values, "Currency"))
                ?.ToUpperInvariant();

        var description =
            NormalizeOptional(
                GetValue(values, "Description"));

        RequireValue(
            accountNumber,
            "AccountNumber",
            errors);
        RequireValue(
            externalReference,
            "ExternalReference",
            errors);
        RequireValue(
            currency,
            "Currency",
            errors);
        RequireValue(
            description,
            "Description",
            errors);

        ValidateLength(
            accountNumber,
            "AccountNumber",
            100,
            errors);
        ValidateLength(
            externalReference,
            "ExternalReference",
            100,
            errors);
        ValidateLength(
            legalEntityCode,
            "LegalEntityCode",
            50,
            errors);
        ValidateLength(
            businessUnitCode,
            "BusinessUnitCode",
            50,
            errors);
        ValidateLength(
            description,
            "Description",
            500,
            errors);

        if (currency is not null &&
            (currency.Length != 3 ||
             !currency.All(char.IsLetter)))
        {
            errors.Add(
                "Currency must be a three-letter " +
                "alphabetic code.");
        }

        Account? account = null;

        if (accountNumber is not null)
        {
            accounts.TryGetValue(
                accountNumber.ToUpperInvariant(),
                out account);

            if (account is null)
            {
                errors.Add(
                    $"AccountNumber '{accountNumber}' " +
                    "does not exist in this organization.");
            }
            else
            {
                ValidateAccountAndDimensions(
                    account,
                    currency,
                    ref legalEntityCode,
                    ref businessUnitCode,
                    errors);
            }
        }

        var row =
            new HistoricalTransactionImportRow
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    batch.OrganizationId,
                BatchId = batch.Id,
                RowNumber =
                    parsedRow.RowNumber,
                ExternalReference =
                    TruncateForStorage(
                        externalReference,
                        100),
                AccountNumber =
                    TruncateForStorage(
                        accountNumber,
                        100) ?? string.Empty,
                AccountId = account?.Id,
                LegalEntityCode =
                    TruncateForStorage(
                        legalEntityCode,
                        50),
                LegalEntityId =
                    account?.LegalEntityId,
                BusinessUnitCode =
                    TruncateForStorage(
                        businessUnitCode,
                        50),
                BusinessUnitId =
                    account?.BusinessUnitId,
                Currency =
                    TruncateForStorage(
                        currency,
                        3),
                Description =
                    TruncateForStorage(
                        description,
                        500),
                RawDataJson =
                    JsonSerializer.Serialize(
                        values,
                        JsonOptions),
                CreatedAtUtc = now
            };

        if (mode == HistoricalImportModes
                .HistoricalTransactions)
        {
            ValidateHistoricalTransaction(
                row,
                values,
                errors,
                now);
        }
        else
        {
            ValidateOpeningBalance(
                row,
                values,
                account,
                errors,
                now);
        }

        row.Fingerprint =
            BuildFingerprint(
                row,
                mode);

        SetErrors(row, errors);

        return row;
    }

    private static void
        ValidateHistoricalTransaction(
            HistoricalTransactionImportRow row,
            IReadOnlyDictionary<string, string>
                values,
            ICollection<string> errors,
            DateTime now)
    {
        row.TransactionDateUtc =
            ParseRequiredDate(
                GetValue(
                    values,
                    "TransactionDateUtc"),
                "TransactionDateUtc",
                errors);

        row.ValueDateUtc =
            ParseOptionalDate(
                GetValue(
                    values,
                    "ValueDateUtc"),
                "ValueDateUtc",
                errors);

        row.Amount =
            ParseRequiredDecimal(
                GetValue(values, "Amount"),
                "Amount",
                errors);

        if (row.Amount.HasValue &&
            row.Amount.Value <= 0)
        {
            errors.Add(
                "Amount must be greater than zero.");
        }

        var direction =
            NormalizeOptional(
                GetValue(values, "Direction"));

        if (string.Equals(
                direction,
                HistoricalTransactionDirections
                    .Credit,
                StringComparison.OrdinalIgnoreCase))
        {
            row.Direction =
                HistoricalTransactionDirections
                    .Credit;
        }
        else if (string.Equals(
                     direction,
                     HistoricalTransactionDirections
                         .Debit,
                     StringComparison.OrdinalIgnoreCase))
        {
            row.Direction =
                HistoricalTransactionDirections.Debit;
        }
        else
        {
            errors.Add(
                "Direction must be either 'Credit' " +
                "or 'Debit'.");
        }

        row.TransactionType =
            NormalizeOptional(
                GetValue(
                    values,
                    "TransactionType"));

        row.Category =
            NormalizeOptional(
                GetValue(values, "Category"));

        row.CounterpartyName =
            NormalizeOptional(
                GetValue(
                    values,
                    "CounterpartyName"));

        RequireValue(
            row.TransactionType,
            "TransactionType",
            errors);
        ValidateLength(
            row.TransactionType,
            "TransactionType",
            100,
            errors);
        ValidateLength(
            row.Category,
            "Category",
            100,
            errors);
        ValidateLength(
            row.CounterpartyName,
            "CounterpartyName",
            200,
            errors);

        row.TransactionType =
            TruncateForStorage(
                row.TransactionType,
                100);
        row.Category =
            TruncateForStorage(
                row.Category,
                100);
        row.CounterpartyName =
            TruncateForStorage(
                row.CounterpartyName,
                200);

        if (row.TransactionDateUtc > now)
        {
            errors.Add(
                "TransactionDateUtc cannot be in " +
                "the future.");
        }

        if (row.ValueDateUtc.HasValue &&
            row.ValueDateUtc.Value > now)
        {
            errors.Add(
                "ValueDateUtc cannot be in the future.");
        }
    }

    private static void ValidateOpeningBalance(
        HistoricalTransactionImportRow row,
        IReadOnlyDictionary<string, string> values,
        Account? account,
        ICollection<string> errors,
        DateTime now)
    {
        row.TransactionDateUtc =
            ParseRequiredDate(
                GetValue(
                    values,
                    "CutoverDateUtc"),
                "CutoverDateUtc",
                errors);

        row.Amount =
            ParseRequiredDecimal(
                GetValue(
                    values,
                    "OpeningBalance"),
                "OpeningBalance",
                errors);

        row.TransactionType = "OpeningBalance";

        if (row.Amount.HasValue &&
            row.Amount.Value <= 0)
        {
            errors.Add(
                "OpeningBalance must be greater than " +
                "zero.");
        }

        if (row.TransactionDateUtc > now)
        {
            errors.Add(
                "CutoverDateUtc cannot be in the future.");
        }

        if (account is not null &&
            (account.Balance != 0 ||
             account.ReservedBalance != 0))
        {
            errors.Add(
                "Cutover opening balance requires an " +
                "account whose current and reserved " +
                "balances are both zero.");
        }
    }

    private static void ValidateAccountAndDimensions(
        Account account,
        string? currency,
        ref string? legalEntityCode,
        ref string? businessUnitCode,
        ICollection<string> errors)
    {
        if (!account.IsActive)
        {
            errors.Add(
                $"Account '{account.AccountNumber}' " +
                "is inactive.");
        }

        if (currency is not null &&
            !string.Equals(
                currency,
                account.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"Currency '{currency}' does not " +
                $"match account currency " +
                $"'{account.Currency}'.");
        }

        if (account.LegalEntity is not null)
        {
            if (legalEntityCode is null)
            {
                legalEntityCode =
                    account.LegalEntity.Code;
            }
            else if (!string.Equals(
                         legalEntityCode,
                         account.LegalEntity.Code,
                         StringComparison
                             .OrdinalIgnoreCase))
            {
                errors.Add(
                    $"LegalEntityCode " +
                    $"'{legalEntityCode}' does not " +
                    "match the account's legal entity " +
                    $"'{account.LegalEntity.Code}'.");
            }

            if (!account.LegalEntity.IsActive)
            {
                errors.Add(
                    "The account's legal entity is " +
                    "inactive.");
            }
        }
        else if (legalEntityCode is not null)
        {
            errors.Add(
                "LegalEntityCode was supplied, but " +
                "the account has no legal entity.");
        }

        if (account.BusinessUnit is not null)
        {
            if (businessUnitCode is null)
            {
                businessUnitCode =
                    account.BusinessUnit.Code;
            }
            else if (!string.Equals(
                         businessUnitCode,
                         account.BusinessUnit.Code,
                         StringComparison
                             .OrdinalIgnoreCase))
            {
                errors.Add(
                    $"BusinessUnitCode " +
                    $"'{businessUnitCode}' does not " +
                    "match the account's business unit " +
                    $"'{account.BusinessUnit.Code}'.");
            }

            if (!account.BusinessUnit.IsActive)
            {
                errors.Add(
                    "The account's business unit is " +
                    "inactive.");
            }
        }
        else if (businessUnitCode is not null)
        {
            errors.Add(
                "BusinessUnitCode was supplied, but " +
                "the account has no business unit.");
        }
    }

    private static void
        ApplyCutoverActivityValidation(
            IEnumerable<
                HistoricalTransactionImportRow> rows,
            string mode,
            IReadOnlySet<Guid>
                accountsWithActivity)
    {
        if (mode != HistoricalImportModes
                .CutoverOpeningBalances)
        {
            return;
        }

        foreach (var row in rows.Where(row =>
                     row.AccountId.HasValue &&
                     accountsWithActivity.Contains(
                         row.AccountId.Value)))
        {
            AddError(
                row,
                "Cutover opening balance requires an " +
                "account with no ledger entries or " +
                "treasury transactions.");
        }
    }

    private static void
        ApplyWithinBatchDuplicateValidation(
            IEnumerable<
                HistoricalTransactionImportRow> rows)
    {
        foreach (var group in rows
                     .Where(row => row.IsValid)
                     .GroupBy(
                         row => row.Fingerprint,
                         StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            foreach (var row in group)
            {
                AddError(
                    row,
                    "This row duplicates another row " +
                    "in the same CSV batch.");
            }
        }
    }

    private static void
        ApplyPriorDuplicateValidation(
            IEnumerable<
                HistoricalTransactionImportRow> rows,
            IReadOnlySet<string> priorFingerprints)
    {
        foreach (var row in rows.Where(row =>
                     row.IsValid &&
                     priorFingerprints.Contains(
                         row.Fingerprint)))
        {
            AddError(
                row,
                "This financial record already exists " +
                "in a previously validated batch.");
        }
    }

    private static void ValidateConcurrencyToken(
        Guid concurrencyToken)
    {
        if (concurrencyToken == Guid.Empty)
        {
            throw new RequestValidationException(
                "A non-empty concurrency token is " +
                "required.");
        }
    }

    private static void ValidateOptionalComment(
        string? comment)
    {
        if (comment?.Trim().Length > 500)
        {
            throw new RequestValidationException(
                "The review comment cannot exceed " +
                "500 characters.");
        }
    }

    private static void EnsurePendingApproval(
        HistoricalTransactionImportBatch batch)
    {
        if (batch.Status !=
            HistoricalImportStatuses.PendingApproval)
        {
            throw new ConflictException(
                "Only a pending historical import " +
                "batch can be reviewed.");
        }
    }

    private void EnsureReviewerIsIndependent(
        HistoricalTransactionImportBatch batch)
    {
        if (batch.UploadedByUserId ==
            _currentUserService.UserId)
        {
            throw new ForbiddenOperationException(
                "The user who uploaded the batch " +
                "cannot approve or reject it.");
        }
    }

    private static string NormalizeApproverRole(
        string mode,
        string currentRole)
    {
        if (mode == HistoricalImportModes
                .CutoverOpeningBalances)
        {
            if (string.Equals(
                    currentRole,
                    Roles.Admin,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                return Roles.Admin;
            }

            if (string.Equals(
                    currentRole,
                    Roles.CFO,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                return Roles.CFO;
            }

            throw new ForbiddenOperationException(
                "Cutover opening balances can only be " +
                "reviewed by an Admin or CFO.");
        }

        if (string.Equals(
                currentRole,
                Roles.Admin,
                StringComparison.OrdinalIgnoreCase))
        {
            return Roles.Admin;
        }

        if (string.Equals(
                currentRole,
                Roles.FinanceManager,
                StringComparison.OrdinalIgnoreCase))
        {
            return Roles.FinanceManager;
        }

        if (string.Equals(
                currentRole,
                Roles.CFO,
                StringComparison.OrdinalIgnoreCase))
        {
            return Roles.CFO;
        }

        throw new ForbiddenOperationException(
            "Historical transactions can only be " +
            "reviewed by an Admin, Finance Manager " +
            "or CFO.");
    }

    private static bool HasRequiredApprovals(
        HistoricalTransactionImportBatch batch,
        IEnumerable<
            HistoricalTransactionImportDecision>
            approvedDecisions)
    {
        var decisions = approvedDecisions
            .Where(item =>
                item.Decision ==
                    ApprovalDecisionTypes.Approved)
            .ToArray();

        if (batch.Mode ==
            HistoricalImportModes
                .CutoverOpeningBalances)
        {
            return decisions.Any(item =>
                       item.ApproverRole ==
                           Roles.Admin) &&
                   decisions.Any(item =>
                       item.ApproverRole ==
                           Roles.CFO);
        }

        return decisions.Any(item =>
            item.ApproverRole is
                Roles.Admin or
                Roles.FinanceManager or
                Roles.CFO);
    }

    private static void CreateHistoricalRecords(
        HistoricalTransactionImportBatch batch,
        IReadOnlyDictionary<Guid, Account> accounts,
        DateTime committedAtUtc,
        ICollection<
            HistoricalTransactionRecord> records)
    {
        foreach (var row in batch.Rows)
        {
            var account =
                RequireCommitAccount(
                    row,
                    accounts);

            EnsureRowStillMatchesAccount(
                row,
                account);

            records.Add(
                new HistoricalTransactionRecord
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        batch.OrganizationId,
                    BatchId = batch.Id,
                    SourceRowId = row.Id,
                    ExternalReference =
                        row.ExternalReference ??
                        throw new ConflictException(
                            $"Row {row.RowNumber} has " +
                            "no external reference."),
                    AccountId = account.Id,
                    LegalEntityId =
                        account.LegalEntityId,
                    BusinessUnitId =
                        account.BusinessUnitId,
                    TransactionDateUtc =
                        row.TransactionDateUtc ??
                        throw new ConflictException(
                            $"Row {row.RowNumber} has " +
                            "no transaction date."),
                    ValueDateUtc =
                        row.ValueDateUtc,
                    Amount = RequirePositiveAmount(row),
                    Currency =
                        account.Currency
                            .ToUpperInvariant(),
                    Direction =
                        row.Direction ??
                        throw new ConflictException(
                            $"Row {row.RowNumber} has " +
                            "no direction."),
                    TransactionType =
                        row.TransactionType ??
                        throw new ConflictException(
                            $"Row {row.RowNumber} has " +
                            "no transaction type."),
                    Description =
                        row.Description ??
                        throw new ConflictException(
                            $"Row {row.RowNumber} has " +
                            "no description."),
                    Category = row.Category,
                    CounterpartyName =
                        row.CounterpartyName,
                    CommittedAtUtc =
                        committedAtUtc,
                    CommittedByUserId =
                        batch.CommittedByUserId ??
                        Guid.Empty
                });
        }
    }

    private async Task CreateOpeningBalancePostings(
        HistoricalTransactionImportBatch batch,
        IReadOnlyDictionary<Guid, Account> accounts,
        DateTime committedAtUtc,
        ICollection<TreasuryTransaction>
            transactions,
        ICollection<LedgerEntry> ledgerEntries)
    {
        var accountsWithActivity =
            await _repository
                .GetAccountIdsWithFinancialActivity(
                    accounts.Keys.ToArray());

        foreach (var row in batch.Rows)
        {
            var account =
                RequireCommitAccount(
                    row,
                    accounts);

            EnsureRowStillMatchesAccount(
                row,
                account);

            if (account.Balance != 0 ||
                account.ReservedBalance != 0)
            {
                throw new ConflictException(
                    $"Account '{account.AccountNumber}' " +
                    "no longer has zero current and " +
                    "reserved balances.");
            }

            if (accountsWithActivity.Contains(
                    account.Id))
            {
                throw new ConflictException(
                    $"Account '{account.AccountNumber}' " +
                    "now has financial activity and " +
                    "cannot receive a cutover opening " +
                    "balance.");
            }

            var amount = RequirePositiveAmount(row);
            var transactionDate =
                row.TransactionDateUtc ??
                throw new ConflictException(
                    $"Row {row.RowNumber} has no " +
                    "cutover date.");

            var transaction =
                new TreasuryTransaction
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        batch.OrganizationId,
                    Reference =
                        TransactionReferenceGenerator
                            .Generate(),
                    TransactionType =
                        TransactionTypes
                            .OpeningBalance,
                    Status =
                        TransactionStatuses.Completed,
                    Amount = amount,
                    Currency =
                        account.Currency
                            .ToUpperInvariant(),
                    Description =
                        row.Description ??
                        "Cutover opening balance",
                    DestinationAccountId =
                        account.Id,
                    InitiatedByUserId =
                        batch.SubmittedByUserId ??
                        batch.UploadedByUserId,
                    CompletedByUserId =
                        _currentUserService.UserId,
                    ExternalReference =
                        row.ExternalReference,
                    Category = "OpeningBalance",
                    IdempotencyKey =
                        $"historical-import:" +
                        $"{batch.Id:N}:" +
                        $"{row.RowNumber}",
                    CreatedAtUtc =
                        transactionDate,
                    CompletedAtUtc =
                        transactionDate
                };

            account.Balance = amount;
            account.ConcurrencyToken =
                Guid.NewGuid();

            row.PostedTreasuryTransactionId =
                transaction.Id;

            transactions.Add(transaction);
            ledgerEntries.Add(
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        batch.OrganizationId,
                    AccountId = account.Id,
                    TreasuryTransactionId =
                        transaction.Id,
                    Amount = amount,
                    EntryType = "Debit",
                    Description =
                        transaction.Description,
                    CreatedAt = transactionDate
                });
        }
    }

    private static Account RequireCommitAccount(
        HistoricalTransactionImportRow row,
        IReadOnlyDictionary<Guid, Account> accounts)
    {
        if (!row.AccountId.HasValue ||
            !accounts.TryGetValue(
                row.AccountId.Value,
                out var account))
        {
            throw new ConflictException(
                $"Row {row.RowNumber} no longer maps " +
                "to an account in this organization.");
        }

        return account;
    }

    private static void EnsureRowStillMatchesAccount(
        HistoricalTransactionImportRow row,
        Account account)
    {
        if (!account.IsActive)
        {
            throw new ConflictException(
                $"Account '{account.AccountNumber}' is " +
                "now inactive.");
        }

        if (!string.Equals(
                row.Currency,
                account.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                $"Account '{account.AccountNumber}' " +
                "currency no longer matches the batch.");
        }

        if (row.LegalEntityId !=
                account.LegalEntityId ||
            row.BusinessUnitId !=
                account.BusinessUnitId)
        {
            throw new ConflictException(
                $"Account '{account.AccountNumber}' " +
                "organization dimensions changed after " +
                "validation.");
        }

        if (account.LegalEntity is not null &&
            !account.LegalEntity.IsActive)
        {
            throw new ConflictException(
                $"Account '{account.AccountNumber}' has " +
                "an inactive legal entity.");
        }

        if (account.BusinessUnit is not null &&
            !account.BusinessUnit.IsActive)
        {
            throw new ConflictException(
                $"Account '{account.AccountNumber}' has " +
                "an inactive business unit.");
        }
    }

    private static decimal RequirePositiveAmount(
        HistoricalTransactionImportRow row)
    {
        if (!row.Amount.HasValue ||
            row.Amount.Value <= 0)
        {
            throw new ConflictException(
                $"Row {row.RowNumber} must have a " +
                "positive amount before commit.");
        }

        return row.Amount.Value;
    }

    private Task RecordBatchAudit(
        HistoricalTransactionImportBatch batch,
        string action,
        string summary)
    {
        return _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action = action,
                EntityType =
                    AuditEntityTypes
                        .HistoricalTransactionImportBatch,
                EntityId = batch.Id,
                EntityReference = batch.FileName,
                Summary = summary,
                AfterValues = new
                {
                    batch.Mode,
                    batch.Status,
                    batch.RequiredApprovalCount,
                    batch.ApprovalCount,
                    batch.SubmittedByUserId,
                    batch.SubmittedAtUtc,
                    batch.ApprovedAtUtc,
                    batch.RejectedByUserId,
                    batch.RejectedAtUtc,
                    batch.CommittedByUserId,
                    batch.CommittedAtUtc
                }
            });
    }

    private static string BuildFingerprint(
        HistoricalTransactionImportRow row,
        string mode)
    {
        string canonical;

        if (mode == HistoricalImportModes
                .CutoverOpeningBalances)
        {
            canonical =
                $"{mode}|{row.AccountId?.ToString("N")}";
        }
        else
        {
            canonical =
                $"{mode}|{row.AccountId?.ToString("N")}" +
                $"|{row.ExternalReference?.Trim().ToUpperInvariant()}";
        }

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(canonical)));
    }

    private static ParsedCsvDocument ParseCsv(
        byte[] content,
        IReadOnlyList<string> expectedHeaders)
    {
        string text;

        try
        {
            text = new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                        false,
                    throwOnInvalidBytes: true)
                .GetString(content);
        }
        catch (DecoderFallbackException)
        {
            throw new RequestValidationException(
                "The CSV file must use valid UTF-8 " +
                "encoding.");
        }

        if (text.IndexOf('\0') >= 0)
        {
            throw new RequestValidationException(
                "The CSV file contains unsupported " +
                "null characters.");
        }

        var records = ParseCsvRecords(text);

        while (records.Count > 0 &&
               records[^1].All(
                   string.IsNullOrWhiteSpace))
        {
            records.RemoveAt(records.Count - 1);
        }

        if (records.Count == 0)
        {
            throw new RequestValidationException(
                "A CSV header row is required.");
        }

        var headers = records[0]
            .Select((value, index) =>
                index == 0
                    ? value.Trim().TrimStart('\uFEFF')
                    : value.Trim())
            .ToArray();

        if (headers.Any(
                string.IsNullOrWhiteSpace))
        {
            throw new RequestValidationException(
                "CSV headers cannot be empty.");
        }

        var duplicateHeader =
            headers
                .GroupBy(
                    header => header,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group =>
                    group.Count() > 1);

        if (duplicateHeader is not null)
        {
            throw new RequestValidationException(
                $"CSV header " +
                $"'{duplicateHeader.Key}' is repeated.");
        }

        var missing = expectedHeaders
            .Except(
                headers,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var unexpected = headers
            .Except(
                expectedHeaders,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (missing.Length > 0 ||
            unexpected.Length > 0)
        {
            var parts = new List<string>();

            if (missing.Length > 0)
            {
                parts.Add(
                    "missing: " +
                    string.Join(", ", missing));
            }

            if (unexpected.Length > 0)
            {
                parts.Add(
                    "unexpected: " +
                    string.Join(", ", unexpected));
            }

            throw new RequestValidationException(
                "CSV headers do not match the selected " +
                $"mode ({string.Join("; ", parts)}). " +
                "Download the matching template.");
        }

        var rows = new List<ParsedCsvRow>();

        for (var index = 1;
             index < records.Count;
             index++)
        {
            var record = records[index];

            if (record.All(
                    string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var values =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            for (var column = 0;
                 column < headers.Length;
                 column++)
            {
                values[headers[column]] =
                    column < record.Count
                        ? record[column].Trim()
                        : string.Empty;
            }

            rows.Add(
                new ParsedCsvRow(
                    RowNumber: index + 1,
                    Values: values,
                    ColumnCountMismatch:
                        record.Count !=
                        headers.Length,
                    ActualColumnCount:
                        record.Count,
                    ExpectedColumnCount:
                        headers.Length));
        }

        return new ParsedCsvDocument(rows);
    }

    private static List<List<string>>
        ParseCsvRecords(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var insideQuotes = false;
        var afterClosingQuote = false;

        for (var index = 0;
             index < text.Length;
             index++)
        {
            var character = text[index];

            if (insideQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length &&
                        text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        insideQuotes = false;
                        afterClosingQuote = true;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            if (afterClosingQuote &&
                character is not ',' and
                    not '\r' and not '\n' &&
                !char.IsWhiteSpace(character))
            {
                throw new RequestValidationException(
                    "A quoted CSV field contains text " +
                    "after its closing quote.");
            }

            if (character == '"' &&
                field.Length == 0)
            {
                insideQuotes = true;
                afterClosingQuote = false;
                continue;
            }

            if (character == ',')
            {
                record.Add(field.ToString());
                field.Clear();
                afterClosingQuote = false;
                continue;
            }

            if (character is '\r' or '\n')
            {
                if (character == '\r' &&
                    index + 1 < text.Length &&
                    text[index + 1] == '\n')
                {
                    index++;
                }

                record.Add(field.ToString());
                field.Clear();
                records.Add(record);
                record = new List<string>();
                afterClosingQuote = false;
                continue;
            }

            field.Append(character);
        }

        if (insideQuotes)
        {
            throw new RequestValidationException(
                "The CSV contains an unclosed quoted " +
                "field.");
        }

        if (field.Length > 0 ||
            record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        return records;
    }

    private static DateTime? ParseRequiredDate(
        string? value,
        string column,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(
                $"{column} is required.");
            return null;
        }

        return ParseDate(value, column, errors);
    }

    private static DateTime? ParseOptionalDate(
        string? value,
        string column,
        ICollection<string> errors)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : ParseDate(value, column, errors);
    }

    private static DateTime? ParseDate(
        string value,
        string column,
        ICollection<string> errors)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            errors.Add(
                $"{column} must be a valid ISO 8601 " +
                "date or timestamp.");
            return null;
        }

        return parsed.UtcDateTime;
    }

    private static decimal? ParseRequiredDecimal(
        string? value,
        string column,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(
                $"{column} is required.");
            return null;
        }

        if (!decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            errors.Add(
                $"{column} must be a valid decimal " +
                "using '.' as the decimal separator.");
            return null;
        }

        return parsed;
    }

    private static void SetErrors(
        HistoricalTransactionImportRow row,
        IEnumerable<string> errors)
    {
        var distinctErrors = errors
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        row.ValidationErrorsJson =
            JsonSerializer.Serialize(
                distinctErrors,
                JsonOptions);
        row.IsValid = distinctErrors.Length == 0;
    }

    private static void AddError(
        HistoricalTransactionImportRow row,
        string error)
    {
        var errors =
            DeserializeErrors(
                row.ValidationErrorsJson)
                .Append(error);

        SetErrors(row, errors);
    }

    private static IReadOnlyList<string>
        DeserializeErrors(string json)
    {
        return JsonSerializer.Deserialize<string[]>(
                   json,
                   JsonOptions) ??
            Array.Empty<string>();
    }

    private async Task<
        HistoricalTransactionImportBatch>
        RequireBatch(Guid batchId)
    {
        var batch =
            await _repository.GetBatch(batchId);

        return batch ??
            throw new ResourceNotFoundException(
                "Historical import batch not found.");
    }

    private async Task<
        HistoricalTransactionImportBatch>
        RequireBatchForUpdate(Guid batchId)
    {
        var batch =
            await _repository
                .GetBatchForUpdate(batchId);

        return batch ??
            throw new ResourceNotFoundException(
                "Historical import batch not found.");
    }

    private Guid RequireOrganizationId()
    {
        return _currentUserService.OrganizationId ??
            throw new ForbiddenOperationException(
                "A tenant organization context is " +
                "required for historical imports.");
    }

    private void ValidateUpload(
        CreateHistoricalImportDryRunDto dto)
    {
        if (dto.ImportKey == Guid.Empty)
        {
            throw new RequestValidationException(
                "A valid Idempotency-Key is required.");
        }

        if (dto.FileContent.Length == 0)
        {
            throw new RequestValidationException(
                "A non-empty CSV file is required.");
        }

        if (dto.FileContent.Length >
            _options.MaximumFileSizeBytes)
        {
            throw new RequestValidationException(
                $"The CSV file exceeds the configured " +
                $"{_options.MaximumFileSizeBytes} byte " +
                "limit.");
        }

        if (string.IsNullOrWhiteSpace(dto.FileName))
        {
            throw new RequestValidationException(
                "The CSV file name is required.");
        }

        if (!string.Equals(
                Path.GetExtension(dto.FileName),
                ".csv",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                "Only .csv files are accepted.");
        }
    }

    private static string NormalizeMode(
        string? mode)
    {
        if (string.Equals(
                mode,
                HistoricalImportModes
                    .HistoricalTransactions,
                StringComparison.OrdinalIgnoreCase))
        {
            return HistoricalImportModes
                .HistoricalTransactions;
        }

        if (string.Equals(
                mode,
                HistoricalImportModes
                    .CutoverOpeningBalances,
                StringComparison.OrdinalIgnoreCase))
        {
            return HistoricalImportModes
                .CutoverOpeningBalances;
        }

        throw new RequestValidationException(
            "Mode must be either " +
            $"'{"HistoricalTransactions"}' or " +
            $"'{"CutoverOpeningBalances"}'.");
    }

    private static IReadOnlyList<string> GetHeaders(
        string mode)
    {
        return mode == HistoricalImportModes
            .HistoricalTransactions
            ? HistoricalTransactionHeaders
            : CutoverOpeningBalanceHeaders;
    }

    private static string NormalizeFileName(
        string fileName)
    {
        var normalized =
            Path.GetFileName(fileName.Trim());

        if (normalized.Length > 255)
        {
            normalized =
                normalized[..255];
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? TruncateForStorage(
        string? value,
        int maximumLength)
    {
        return value is null ||
            value.Length <= maximumLength
            ? value
            : value[..maximumLength];
    }

    private static string? NeutralizeSpreadsheetFormula(
        string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value[0] is '=' or '+' or '-' or '@' or
            '\t' or '\r'
            ? $"'{value}"
            : value;
    }

    private static string? GetValue(
        IReadOnlyDictionary<string, string> row,
        string column)
    {
        return row.TryGetValue(
            column,
            out var value)
            ? value
            : null;
    }

    private static void RequireValue(
        string? value,
        string column,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{column} is required.");
        }
    }

    private static void ValidateLength(
        string? value,
        string column,
        int maximum,
        ICollection<string> errors)
    {
        if (value is not null &&
            value.Length > maximum)
        {
            errors.Add(
                $"{column} cannot exceed " +
                $"{maximum} characters.");
        }
    }

    private static HistoricalImportBatchResponseDto
        MapBatch(
            HistoricalTransactionImportBatch batch,
            bool isIdempotentReplay)
    {
        return new HistoricalImportBatchResponseDto
        {
            Id = batch.Id,
            ImportKey = batch.ImportKey,
            Mode = batch.Mode,
            Status = batch.Status,
            FileName = batch.FileName,
            FileHash = batch.FileHash,
            TotalRowCount = batch.TotalRowCount,
            ValidRowCount = batch.ValidRowCount,
            InvalidRowCount =
                batch.InvalidRowCount,
            UploadedByUserId =
                batch.UploadedByUserId,
            UploadedAtUtc = batch.UploadedAtUtc,
            ValidatedAtUtc = batch.ValidatedAtUtc,
            SubmittedByUserId =
                batch.SubmittedByUserId,
            SubmittedAtUtc = batch.SubmittedAtUtc,
            RequiredApprovalCount =
                batch.RequiredApprovalCount,
            ApprovalCount = batch.ApprovalCount,
            ApprovedAtUtc = batch.ApprovedAtUtc,
            RejectedByUserId =
                batch.RejectedByUserId,
            RejectedAtUtc = batch.RejectedAtUtc,
            RejectionReason =
                batch.RejectionReason,
            CommittedByUserId =
                batch.CommittedByUserId,
            CommittedAtUtc = batch.CommittedAtUtc,
            ConcurrencyToken =
                batch.ConcurrencyToken,
            IsIdempotentReplay =
                isIdempotentReplay,
            IsPostingOperation = false,
            NextAction = GetNextAction(batch)
        };
    }

    private static HistoricalImportRowResponseDto
        MapRow(HistoricalTransactionImportRow row)
    {
        return new HistoricalImportRowResponseDto
        {
            Id = row.Id,
            RowNumber = row.RowNumber,
            ExternalReference =
                row.ExternalReference,
            AccountNumber = row.AccountNumber,
            AccountId = row.AccountId,
            LegalEntityCode =
                row.LegalEntityCode,
            LegalEntityId = row.LegalEntityId,
            BusinessUnitCode =
                row.BusinessUnitCode,
            BusinessUnitId = row.BusinessUnitId,
            TransactionDateUtc =
                row.TransactionDateUtc,
            ValueDateUtc = row.ValueDateUtc,
            Amount = row.Amount,
            Currency = row.Currency,
            Direction = row.Direction,
            TransactionType = row.TransactionType,
            Description = row.Description,
            Category = row.Category,
            CounterpartyName =
                row.CounterpartyName,
            IsValid = row.IsValid,
            PostedTreasuryTransactionId =
                row.PostedTreasuryTransactionId,
            ValidationErrors =
                DeserializeErrors(
                    row.ValidationErrorsJson)
        };
    }

    private static string GetNextAction(
        HistoricalTransactionImportBatch batch)
    {
        return batch.Status switch
        {
            HistoricalImportStatuses
                .ValidationFailed =>
                "Correct the error report and upload " +
                "a revised file with a new " +
                "Idempotency-Key.",

            HistoricalImportStatuses.Validated =>
                "Submit the validated batch for " +
                "independent approval.",

            HistoricalImportStatuses
                .PendingApproval =>
                $"Await {Math.Max(
                    0,
                    batch.RequiredApprovalCount -
                    batch.ApprovalCount)} more " +
                "required approval(s).",

            HistoricalImportStatuses.Approved =>
                "An organization Admin can now commit " +
                "the approved batch.",

            HistoricalImportStatuses.Rejected =>
                "This batch is closed. Correct the " +
                "source data and upload a new batch.",

            HistoricalImportStatuses.Committed =>
                "The batch is committed and cannot be " +
                "committed again.",

            _ => string.Empty
        };
    }

    private static
        HistoricalImportDecisionResponseDto MapDecision(
            HistoricalTransactionImportDecision
                decision)
    {
        return new
            HistoricalImportDecisionResponseDto
            {
                Id = decision.Id,
                ApproverUserId =
                    decision.ApproverUserId,
                ApproverName =
                    decision.ApproverUser is null
                        ? string.Empty
                        : ($"{decision.ApproverUser.FirstName} " +
                           $"{decision.ApproverUser.LastName}")
                            .Trim(),
                ApproverRole =
                    decision.ApproverRole,
                Decision = decision.Decision,
                Comment = decision.Comment,
                CreatedAtUtc =
                    decision.CreatedAtUtc
            };
    }

    private static
        HistoricalTransactionRecordResponseDto
        MapHistoricalRecord(
            HistoricalTransactionRecord record)
    {
        return new
            HistoricalTransactionRecordResponseDto
            {
                Id = record.Id,
                BatchId = record.BatchId,
                ExternalReference =
                    record.ExternalReference,
                AccountId = record.AccountId,
                AccountNumber =
                    record.Account.AccountNumber,
                LegalEntityId =
                    record.LegalEntityId,
                BusinessUnitId =
                    record.BusinessUnitId,
                TransactionDateUtc =
                    record.TransactionDateUtc,
                ValueDateUtc = record.ValueDateUtc,
                Amount = record.Amount,
                Currency = record.Currency,
                Direction = record.Direction,
                TransactionType =
                    record.TransactionType,
                Description = record.Description,
                Category = record.Category,
                CounterpartyName =
                    record.CounterpartyName,
                CommittedAtUtc =
                    record.CommittedAtUtc,
                CommittedByUserId =
                    record.CommittedByUserId
            };
    }

    private sealed record ParsedCsvDocument(
        IReadOnlyList<ParsedCsvRow> Rows);

    private sealed record ParsedCsvRow(
        int RowNumber,
        IReadOnlyDictionary<string, string> Values,
        bool ColumnCountMismatch,
        int ActualColumnCount,
        int ExpectedColumnCount);
}
