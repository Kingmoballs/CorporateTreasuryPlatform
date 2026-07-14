using Treasury.Application.DTOs.InvestmentPlacements;

namespace Treasury.Application.Interfaces;

public interface IInvestmentPlacementService
{
    Task<InvestmentPlacementResponseDto> Create(
        CreateInvestmentPlacementDto dto);

    Task<InvestmentPlacementResponseDto> GetById(
        Guid id);

    Task<PagedInvestmentPlacementResponseDto> Search(
        InvestmentPlacementQueryDto query);
    
    Task<InvestmentPlacementResponseDto> Activate(
        Guid id,
        string idempotencyKey);

    Task<InvestmentPlacementResponseDto>
        ApproveActivation(Guid id);

    Task<InvestmentPlacementResponseDto>
        RejectActivation(
            Guid id,
            string reason);

    Task<InvestmentPlacementResponseDto> Cancel(
        Guid id,
        string reason);
}