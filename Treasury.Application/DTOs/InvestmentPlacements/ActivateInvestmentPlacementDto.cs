namespace Treasury.Application.DTOs.InvestmentPlacements;

public class ActivateInvestmentPlacementDto
{
    /*
     * Generate this value once on the client and reuse it
     * when retrying the same activation request.
     */
    public string IdempotencyKey { get; set; } =
        string.Empty;
}