using Treasury.Application.DTOs.TreasuryAlerts;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface ITreasuryAlertRepository
{
    Task Add(TreasuryAlert alert);

    Task<TreasuryAlert?> GetById(Guid id);

    Task<(IReadOnlyList<TreasuryAlert> Items, int TotalCount)> Search(
        TreasuryAlertQueryDto query);

    void Update(TreasuryAlert alert);

    Task<bool> OpenAlertExists(
        string alertType,
        string? sourceEntityType,
        Guid? sourceEntityId,
        string? sourceReference);
    
    Task<IReadOnlyList<TreasuryAlert>> GetForSummary(
        TreasuryAlertSummaryQueryDto query);
    
    Task<IReadOnlyList<TreasuryAlert>> GetForExport(
        TreasuryAlertQueryDto query,
        int maxRows);
    
    Task<TreasuryAlert?> GetOpenAlert(
        string alertType,
        string? sourceEntityType,
        Guid? sourceEntityId,
        string? sourceReference);

    Task SaveChanges();
}