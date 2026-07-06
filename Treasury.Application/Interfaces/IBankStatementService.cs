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
}