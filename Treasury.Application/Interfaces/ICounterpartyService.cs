using Treasury.Application.DTOs.Counterparties;

namespace Treasury.Application.Interfaces;

public interface ICounterpartyService
{
    Task<CounterpartyResponseDto> Create(
        CreateCounterpartyDto dto);

    Task<CounterpartyResponseDto> GetById(
        Guid id);

    Task<PagedCounterpartyResponseDto> Search(
        CounterpartyQueryDto query);

    Task<CounterpartyResponseDto> Update(
        Guid id,
        UpdateCounterpartyDto dto);

    Task<CounterpartyResponseDto> SetStatus(
        Guid id,
        bool isActive);
}