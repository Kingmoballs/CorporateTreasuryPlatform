using Treasury.Application.DTOs.InvestmentPlacements;

namespace Treasury.Application.Interfaces;

public interface IInvestmentEarlyRedemptionRequestService
{
    Task<InvestmentEarlyRedemptionRequestResponseDto>
        Create(
            Guid investmentPlacementId,
            CreateInvestmentEarlyRedemptionRequestDto dto);

    Task<InvestmentEarlyRedemptionRequestResponseDto>
        GetById(Guid id);

    Task<List<InvestmentEarlyRedemptionRequestResponseDto>>
        GetPending();

    Task<InvestmentEarlyRedemptionRequestResponseDto>
        Approve(Guid id);

    Task<InvestmentEarlyRedemptionRequestResponseDto>
        Reject(
            Guid id,
            string reason);
    

    Task<InvestmentEarlyRedemptionRequestResponseDto>
        Execute(Guid id);
}