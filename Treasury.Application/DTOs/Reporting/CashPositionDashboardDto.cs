namespace Treasury.Application.DTOs.Reporting;

public class CashPositionDashboardDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public int ActiveAccountCount { get; set; }

    public int PendingApprovalCount { get; set; }

    public IReadOnlyList<CurrencyCashPositionDto>
        Positions { get; set; }
        = Array.Empty<CurrencyCashPositionDto>();
}
