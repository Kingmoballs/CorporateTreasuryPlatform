using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IBankStatementRepository
{
    Task AddImport(BankStatementImport statementImport);

    Task<BankStatementImport?> GetImportById(Guid importId);

    Task<BankStatementLine?> GetLineById(Guid lineId);

    Task<List<BankStatementLine>> GetUnmatchedLines(
        Guid? accountId,
        DateTime? fromUtc,
        DateTime? toUtc);

    void UpdateLine(BankStatementLine line);

    Task<bool> StatementReferenceExists(
        Guid accountId,
        string statementReference);

    Task SaveChanges();
}