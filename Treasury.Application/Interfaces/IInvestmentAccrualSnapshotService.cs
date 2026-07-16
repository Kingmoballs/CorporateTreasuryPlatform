using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.DTOs.Exports;

namespace Treasury.Application.Interfaces;

public interface IInvestmentAccrualSnapshotService
{
    Task<InvestmentAccrualSnapshotGenerationResultDto>
        Generate(
            GenerateInvestmentAccrualSnapshotsDto dto);

    Task<PagedInvestmentAccrualSnapshotResponseDto>
        Search(
            InvestmentAccrualSnapshotQueryDto query);

    Task<CsvExportDto> ExportCsv(
        InvestmentAccrualSnapshotQueryDto query,
        int maxRows = 5000);
}