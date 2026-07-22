namespace Treasury.Application.DTOs.CreditFacilityRepayments;

public class CreateCreditFacilityRepaymentDto
{
    /*
     * Total cash payment. The service automatically
     * allocates it to interest first, then principal.
     */
    public decimal Amount { get; set; }

    public string? ExternalReference { get; set; }

    /*
     * Generate once and reuse when retrying
     * the same repayment.
     */
    public string IdempotencyKey { get; set; } =
        string.Empty;

    public string? Description { get; set; }
}