using Treasury.Application.DTOs.InvestmentLimits;

namespace Treasury.Application.Interfaces;

public interface IInvestmentLimitService
{
    Task<InvestmentLimitResponseDto> Create(
        CreateInvestmentLimitDto dto);

    Task<InvestmentLimitResponseDto> GetById(
        Guid id);

    Task<PagedInvestmentLimitResponseDto> Search(
        InvestmentLimitQueryDto query);

    Task<InvestmentLimitResponseDto> Update(
        Guid id,
        UpdateInvestmentLimitDto dto);

    Task<InvestmentLimitResponseDto> SetStatus(
        Guid id,
        bool isActive);
}