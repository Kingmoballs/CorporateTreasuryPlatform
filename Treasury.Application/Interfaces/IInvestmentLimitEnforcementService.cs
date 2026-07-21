namespace Treasury.Application.Interfaces;

public interface IInvestmentLimitEnforcementService
{
    Task EnsureWithinLimits(
        Guid counterpartyId,
        string currency,
        string investmentType,
        decimal proposedPrincipalAmount,
        Guid? excludedPlacementId);
}