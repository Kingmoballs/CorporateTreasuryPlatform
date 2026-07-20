using Treasury.Application.DTOs.InvestmentPlacements;

namespace Treasury.Application.Interfaces;

public interface IInvestmentRolloverRequestService
{
    Task<InvestmentRolloverRequestResponseDto> Create(
        Guid investmentPlacementId,
        CreateInvestmentRolloverRequestDto dto);

    Task<InvestmentRolloverRequestResponseDto> GetById(
        Guid id);

    Task<List<InvestmentRolloverRequestResponseDto>>
        GetPending();

    Task<InvestmentRolloverRequestResponseDto> Approve(
        Guid id);

    Task<InvestmentRolloverRequestResponseDto> Reject(
        Guid id,
        string reason);
}