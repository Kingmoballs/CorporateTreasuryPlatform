using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Repositories;

public class BankStatementRepository
    : IBankStatementRepository
{
    private readonly TreasuryDbContext _context;

    public BankStatementRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task AddImport(
        BankStatementImport statementImport)
    {
        await _context.BankStatementImports
            .AddAsync(statementImport);
    }

    public async Task<BankStatementImport?> GetImportById(
        Guid importId)
    {
        return await _context.BankStatementImports
            .Include(statementImport =>
                statementImport.Account)
            .Include(statementImport =>
                statementImport.UploadedByUser)
            .Include(statementImport =>
                statementImport.Lines)
            .ThenInclude(line =>
                line.MatchedTreasuryTransaction)
            .FirstOrDefaultAsync(statementImport =>
                statementImport.Id == importId);
    }

    public async Task<BankStatementLine?> GetLineById(
        Guid lineId)
    {
        return await _context.BankStatementLines
            .Include(line =>
                line.Account)
            .Include(line =>
                line.MatchedTreasuryTransaction)
            .FirstOrDefaultAsync(line =>
                line.Id == lineId);
    }

    public async Task<List<BankStatementLine>> GetUnmatchedLines(
        Guid? accountId,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        var query =
            _context.BankStatementLines
                .AsQueryable();

        query =
            query.Where(line =>
                line.ReconciliationStatus ==
                ReconciliationStatus.Unmatched);

        if (accountId.HasValue)
        {
            query =
                query.Where(line =>
                    line.AccountId == accountId.Value);
        }

        if (fromUtc.HasValue)
        {
            query =
                query.Where(line =>
                    line.TransactionDateUtc >=
                    fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query =
                query.Where(line =>
                    line.TransactionDateUtc <=
                    toUtc.Value);
        }

        return await query
            .OrderBy(line =>
                line.TransactionDateUtc)
            .ThenBy(line =>
                line.LineNumber)
            .ToListAsync();
    }

    public void UpdateLine(
        BankStatementLine line)
    {
        _context.BankStatementLines.Update(line);
    }

    public async Task<bool> StatementReferenceExists(
        Guid accountId,
        string statementReference)
    {
        return await _context.BankStatementImports
            .AnyAsync(statementImport =>
                statementImport.AccountId == accountId &&
                statementImport.StatementReference ==
                    statementReference);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}