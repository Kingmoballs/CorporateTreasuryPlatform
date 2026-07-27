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
        DateTime? toUtc,
        Guid? legalEntityId = null,
        Guid? businessUnitId = null);

    void UpdateLine(BankStatementLine line);

    Task<bool> StatementReferenceExists(
        Guid accountId,
        string statementReference);
    
    Task<bool> TransactionAlreadyMatched(
        Guid treasuryTransactionId,
        Guid? excludeLineId = null);

    Task SaveChanges();
}
