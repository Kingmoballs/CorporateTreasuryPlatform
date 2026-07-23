using System.ComponentModel.DataAnnotations.Schema;

namespace Treasury.Domain.Entities;

public class CreditFacility
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /*
     * Internally generated reference such as:
     * FAC-20260722-000001
     */
    public string Reference { get; set; } =
        string.Empty;

    public string FacilityName { get; set; } =
        string.Empty;

    public string FacilityType { get; set; } =
        "RevolvingCredit";

    public Guid LenderCounterpartyId { get; set; }

    public Counterparty LenderCounterparty { get; set; } =
        null!;

    /*
     * Snapshot of the lender name. This preserves the
     * original name even if the counterparty is renamed.
     */
    public string LenderName { get; set; } =
        string.Empty;

    public Guid SettlementAccountId { get; set; }

    public Account SettlementAccount { get; set; } =
        null!;

    public string Currency { get; set; } =
        string.Empty;

    public decimal ApprovedLimitAmount { get; set; }

    public decimal OutstandingPrincipalAmount
        { get; set; }

    public decimal AccruedInterestAmount
        { get; set; }

    public decimal AnnualInterestRate { get; set; }

    /*
     * Percentage charged on the unused portion
     * of a committed facility.
     */
    public decimal CommitmentFeeRatePercentage
        { get; set; }

    public decimal ArrangementFeeAmount { get; set; }

    public int DayCountBasis { get; set; } = 365;

    public string InterestPaymentFrequency
        { get; set; } = "Monthly";

    public DateTime StartDateUtc { get; set; }

    public DateTime MaturityDateUtc { get; set; }

    public string Status { get; set; } = "Draft";

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public User? UpdatedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } =
        DateTime.UtcNow;

    // Activation approval information
    public int RequiredApprovalCount { get; set; }

    public int ApprovalCount { get; set; }

    public Guid? ActivationRequestedByUserId
        { get; set; }

    public User? ActivationRequestedByUser
        { get; set; }

    public DateTime? ActivationRequestedAtUtc
        { get; set; }

    public DateTime? ActivationExpiresAtUtc
        { get; set; }

    /*
     * Prevents a repeated activation request from
     * creating multiple approval workflows.
     */
    public string? ActivationIdempotencyKey
        { get; set; }

    public Guid? ActivationRejectedByUserId
        { get; set; }

    public User? ActivationRejectedByUser
        { get; set; }

    public DateTime? ActivationRejectedAtUtc
        { get; set; }

    public string? ActivationRejectionReason
        { get; set; }

    public Guid? ActivatedByUserId { get; set; }

    public User? ActivatedByUser { get; set; }

    public DateTime? ActivatedAtUtc { get; set; }

    // Suspension information
    public Guid? SuspendedByUserId { get; set; }

    public User? SuspendedByUser { get; set; }

    public DateTime? SuspendedAtUtc { get; set; }

    public string? SuspensionReason { get; set; }

    // Maturity information
    public DateTime? MaturedAtUtc { get; set; }

    // Closure information
    public Guid? ClosedByUserId { get; set; }

    public User? ClosedByUser { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public string? ClosureReason { get; set; }

    // Cancellation information
    public Guid? CancelledByUserId { get; set; }

    public User? CancelledByUser { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? CancellationReason { get; set; }

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();
    
    public ICollection<CreditFacilityDrawdown>
        Drawdowns { get; set; } =
            new List<CreditFacilityDrawdown>();
    
    public ICollection<CreditFacilityRepayment>
        Repayments { get; set; } =
            new List<CreditFacilityRepayment>();
    
    public ICollection<CreditFacilityInterestAccrualSnapshot>
        InterestAccrualSnapshots { get; set; } =
            new List<CreditFacilityInterestAccrualSnapshot>();

    [NotMapped]
    public decimal AvailableAmount =>
        ApprovedLimitAmount -
        OutstandingPrincipalAmount;

    [NotMapped]
    public decimal TotalOutstandingAmount =>
        OutstandingPrincipalAmount +
        AccruedInterestAmount;
    
}
