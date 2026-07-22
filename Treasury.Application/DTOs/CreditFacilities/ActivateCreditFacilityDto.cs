namespace Treasury.Application.DTOs.CreditFacilities;

public class ActivateCreditFacilityDto
{
    /*
     * Generate this value once and reuse it when
     * retrying the same activation request.
     */
    public string IdempotencyKey { get; set; } =
        string.Empty;
}