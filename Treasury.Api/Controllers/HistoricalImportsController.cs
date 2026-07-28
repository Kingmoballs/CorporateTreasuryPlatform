using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Treasury.Api.DTOs;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.HistoricalImports;
using Treasury.Application.Interfaces;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Api.Controllers;

[ApiController]
[Route("api/v1/historical-imports")]
[Authorize(Roles = ReadRoles)]
public class HistoricalImportsController
    : ControllerBase
{
    private const string ReadRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private const string UploadRoles =
        Roles.Admin + "," +
        Roles.TreasuryOfficer;

    private const string ReviewRoles =
        Roles.Admin + "," +
        Roles.FinanceManager + "," +
        Roles.CFO;

    private readonly IHistoricalTransactionImportService
        _service;

    private readonly HistoricalImportOptions _options;

    public HistoricalImportsController(
        IHistoricalTransactionImportService service,
        IOptions<HistoricalImportOptions> options)
    {
        _service = service;
        _options = options.Value;
    }

    [HttpGet("template")]
    public IActionResult DownloadTemplate(
        [FromQuery] string mode)
    {
        var template = _service.GetTemplate(mode);

        return File(
            template.Content,
            template.ContentType,
            template.FileName);
    }

    [HttpGet]
    public async Task<IActionResult> SearchBatches(
        [FromQuery] HistoricalImportBatchQueryDto query)
    {
        return Ok(
            await _service.SearchBatches(query));
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        return Ok(
            await _service.GetDashboard());
    }

    [HttpPost("dry-run")]
    [Consumes("multipart/form-data")]
    [Authorize(Roles = UploadRoles)]
    public async Task<IActionResult> DryRun(
        [FromForm]
            HistoricalImportCsvUploadRequest request,
        [FromHeader(Name = "Idempotency-Key")]
            string? idempotencyKey)
    {
        if (!Guid.TryParse(
                idempotencyKey,
                out var importKey) ||
            importKey == Guid.Empty)
        {
            throw new RequestValidationException(
                "Idempotency-Key must be a non-empty " +
                "GUID.");
        }

        if (request.File is null ||
            request.File.Length == 0)
        {
            throw new RequestValidationException(
                "A non-empty CSV file is required.");
        }

        if (request.File.Length >
            _options.MaximumFileSizeBytes)
        {
            throw new RequestValidationException(
                $"The CSV file exceeds the configured " +
                $"{_options.MaximumFileSizeBytes} byte " +
                "limit.");
        }

        if (!string.Equals(
                Path.GetExtension(
                    request.File.FileName),
                ".csv",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                "Only .csv files are accepted.");
        }

        await using var stream =
            new MemoryStream();

        await request.File.CopyToAsync(stream);

        var result = await _service.DryRun(
            new CreateHistoricalImportDryRunDto
            {
                ImportKey = importKey,
                Mode = request.Mode,
                FileName =
                    Path.GetFileName(
                        request.File.FileName),
                FileContent = stream.ToArray()
            });

        return result.IsIdempotentReplay
            ? Ok(result)
            : StatusCode(
                StatusCodes.Status201Created,
                result);
    }

    [HttpGet("{batchId:guid}")]
    public async Task<IActionResult> GetBatch(
        Guid batchId)
    {
        return Ok(
            await _service.GetBatch(batchId));
    }

    [HttpGet("{batchId:guid}/rows")]
    public async Task<IActionResult> GetRows(
        Guid batchId,
        [FromQuery] HistoricalImportRowsQueryDto query)
    {
        return Ok(
            await _service.GetRows(
                batchId,
                query));
    }

    [HttpGet("{batchId:guid}/errors")]
    public async Task<IActionResult> GetErrors(
        Guid batchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return Ok(
            await _service.GetRows(
                batchId,
                new HistoricalImportRowsQueryDto
                {
                    Page = page,
                    PageSize = pageSize,
                    IsValid = false
                }));
    }

    [HttpGet("{batchId:guid}/errors/export/csv")]
    public async Task<IActionResult> ExportErrors(
        Guid batchId)
    {
        var export =
            await _service.ExportErrors(batchId);

        return File(
            export.Content,
            export.ContentType,
            export.FileName);
    }

    [HttpPost("{batchId:guid}/submit")]
    [Authorize(Roles = UploadRoles)]
    public async Task<IActionResult> Submit(
        Guid batchId,
        HistoricalImportConcurrencyDto dto)
    {
        return Ok(
            await _service.Submit(
                batchId,
                dto));
    }

    [HttpPost("{batchId:guid}/approve")]
    [Authorize(Roles = ReviewRoles)]
    public async Task<IActionResult> Approve(
        Guid batchId,
        ReviewHistoricalImportDto dto)
    {
        return Ok(
            await _service.Approve(
                batchId,
                dto));
    }

    [HttpPost("{batchId:guid}/reject")]
    [Authorize(Roles = ReviewRoles)]
    public async Task<IActionResult> Reject(
        Guid batchId,
        RejectHistoricalImportDto dto)
    {
        return Ok(
            await _service.Reject(
                batchId,
                dto));
    }

    [HttpGet("{batchId:guid}/decisions")]
    public async Task<IActionResult> GetDecisions(
        Guid batchId)
    {
        return Ok(
            await _service.GetDecisions(batchId));
    }

    [HttpGet("{batchId:guid}/approval-report")]
    public async Task<IActionResult> GetApprovalReport(
        Guid batchId)
    {
        return Ok(
            await _service.GetApprovalReport(batchId));
    }

    [HttpGet(
        "{batchId:guid}/approval-report/export/csv")]
    public async Task<IActionResult>
        ExportApprovalReport(Guid batchId)
    {
        var export =
            await _service.ExportApprovalReport(
                batchId);

        return File(
            export.Content,
            export.ContentType,
            export.FileName);
    }

    [HttpGet(
        "{batchId:guid}/opening-balance-reconciliation")]
    public async Task<IActionResult>
        GetOpeningBalanceReconciliation(
            Guid batchId)
    {
        return Ok(
            await _service
                .GetOpeningBalanceReconciliation(
                    batchId));
    }

    [HttpGet(
        "{batchId:guid}/opening-balance-reconciliation/export/csv")]
    public async Task<IActionResult>
        ExportOpeningBalanceReconciliation(
            Guid batchId)
    {
        var export =
            await _service
                .ExportOpeningBalanceReconciliation(
                    batchId);

        return File(
            export.Content,
            export.ContentType,
            export.FileName);
    }

    [HttpPost("{batchId:guid}/commit")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Commit(
        Guid batchId,
        HistoricalImportConcurrencyDto dto)
    {
        return Ok(
            await _service.Commit(
                batchId,
                dto));
    }

    [HttpGet("records")]
    public async Task<IActionResult> GetCommittedRecords(
        [FromQuery]
            HistoricalTransactionRecordQueryDto query)
    {
        return Ok(
            await _service.GetCommittedRecords(query));
    }

    [HttpGet("records/{recordId:guid}")]
    public async Task<IActionResult> GetCommittedRecord(
        Guid recordId)
    {
        return Ok(
            await _service.GetCommittedRecord(
                recordId));
    }

    [HttpGet("records/export/csv")]
    public async Task<IActionResult>
        ExportCommittedRecords(
            [FromQuery]
                HistoricalTransactionRecordQueryDto query,
            [FromQuery] int maxRows = 5000)
    {
        var export =
            await _service.ExportCommittedRecords(
                query,
                maxRows);

        return File(
            export.Content,
            export.ContentType,
            export.FileName);
    }
}
