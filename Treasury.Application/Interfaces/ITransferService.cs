using Treasury.Application.DTOs.Transfers;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface ITransferService
{
    Task<TransferResponseDto>
        TransferFunds(CreateTransferDto dto);

    Task<List<TransferRequest>>
        GetPendingTransfers();

    Task<TransferResponseDto>
        ApproveTransfer(Guid transferId);

    Task<string> RejectTransfer(
        Guid transferId,
        string reason);
}