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

    Task<CsvExportDto> ExportErrors(
        Guid batchId);
}
