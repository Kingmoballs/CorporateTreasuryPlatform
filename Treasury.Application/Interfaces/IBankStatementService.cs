using Treasury.Application.DTOs.BankStatements;

namespace Treasury.Application.Interfaces;

public interface IBankStatementService
{
    Task<BankStatementImportResponseDto> ImportStatement(
        CreateBankStatementImportDto dto);

    Task<BankStatementImportResponseDto> GetImport(
        Guid importId);

    Task<List<BankStatementLineResponseDto>> GetUnmatchedLines(
        Guid? accountId,
        DateTime? fromUtc,
        DateTime? toUtc);

    Task<BankStatementReconciliationResultDto> AutoMatchImport(
        Guid importId,
        int dateToleranceDays = 2);
    
    Task<BankStatementLineResponseDto> ManualMatchLine(
        Guid lineId,
        Guid treasuryTransactionId);

    Task<BankStatementLineResponseDto> ReconcileLine(
        Guid lineId);

    Task<BankStatementLineResponseDto> UnmatchLine(
        Guid lineId);

    Task<BankStatementLineResponseDto> IgnoreLine(
        Guid lineId);
    
    Task<BankStatementReconciliationSummaryDto>
        GetReconciliationSummary(Guid importId);

    Task<BankStatementExceptionReportDto>
        GetExceptionReport(Guid importId);
    
    Task<BookSideExceptionReportDto>
        GetBookSideExceptionReport(Guid importId);
        
    Task<BankStatementImportResponseDto> ImportStatementFromCsv(
        CreateBankStatementCsvImportDto dto);

    Task<BankStatementImportResponseDto> ImportStatementFromPdf(
        CreateBankStatementPdfImportDto dto);
}