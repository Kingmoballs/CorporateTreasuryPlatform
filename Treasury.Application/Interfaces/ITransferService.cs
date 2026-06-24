using Treasury.Application.DTOs.Transfers;

namespace Treasury.Application.Interfaces;

public interface ITransferService
{
    Task<TransferResponseDto>
        TransferFunds(
            CreateTransferDto dto);
}