using Treasury.Application.DTOs.Exports;
using Treasury.Application.DTOs.HistoricalImports;

namespace Treasury.Application.Interfaces;

public interface IHistoricalTransactionImportService
{
    HistoricalImportTemplateDto GetTemplate(
        string mode);

    Task<HistoricalImportBatchResponseDto> DryRun(
        CreateHistoricalImportDryRunDto dto);

    Task<HistoricalImportBatchResponseDto> GetBatch(
        Guid batchId);

    Task<PagedHistoricalImportBatchesResponseDto>
        SearchBatches(
            HistoricalImportBatchQueryDto query);

    Task<HistoricalImportDashboardResponseDto>
        GetDashboard();

    Task<PagedHistoricalImportRowsResponseDto> GetRows(
        Guid batchId,
        HistoricalImportRowsQueryDto query);

    Task<HistoricalImportBatchResponseDto> Submit(
        Guid batchId,
        HistoricalImportConcurrencyDto dto);

    Task<HistoricalImportBatchResponseDto> Approve(
        Guid batchId,
        ReviewHistoricalImportDto dto);

    Task<HistoricalImportBatchResponseDto> Reject(
        Guid batchId,
        RejectHistoricalImportDto dto);

    Task<IReadOnlyList<
        HistoricalImportDecisionResponseDto>>
        GetDecisions(Guid batchId);

    Task<HistoricalImportCommitResponseDto> Commit(
        Guid batchId,
        HistoricalImportConcurrencyDto dto);

    Task<PagedHistoricalTransactionRecordsResponseDto>
        GetCommittedRecords(
            HistoricalTransactionRecordQueryDto query);

    Task<HistoricalTransactionRecordResponseDto>
        GetCommittedRecord(Guid recordId);

    Task<CsvExportDto> ExportCommittedRecords(
        HistoricalTransactionRecordQueryDto query,
        int maxRows);

    Task<HistoricalImportApprovalReportResponseDto>
        GetApprovalReport(Guid batchId);

    Task<CsvExportDto> ExportApprovalReport(
        Guid batchId);

    Task<
        OpeningBalanceReconciliationReportResponseDto>
        GetOpeningBalanceReconciliation(
            Guid batchId);

    Task<CsvExportDto>
        ExportOpeningBalanceReconciliation(
            Guid batchId);

    Task<CsvExportDto> ExportErrors(
        Guid batchId);
}
