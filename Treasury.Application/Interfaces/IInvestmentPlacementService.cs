using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.DTOs.Exports;

namespace Treasury.Application.Interfaces;

public interface IInvestmentPlacementService
{
    Task<InvestmentPlacementResponseDto> Create(
        CreateInvestmentPlacementDto dto);

    Task<InvestmentPlacementResponseDto> GetById(
        Guid id);
    
    Task<InvestmentPlacementResponseDto>
        AssignCounterparty(
            Guid id,
            Guid counterpartyId);

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
    
    Task<InvestmentMaturityProcessingResultDto>
        ProcessDueMaturities(
            int maxRows = 100);

    Task<InvestmentPlacementResponseDto> Redeem(
        Guid id,
        RedeemInvestmentPlacementDto dto);
    
    Task<InvestmentPortfolioReportDto>
        GetPortfolioReport(
            InvestmentPortfolioQueryDto query);

    Task<InvestmentMaturityScheduleDto>
        GetMaturitySchedule(
            InvestmentPortfolioQueryDto query);

    Task<CsvExportDto> ExportPortfolioCsv(
        InvestmentPortfolioQueryDto query);
}