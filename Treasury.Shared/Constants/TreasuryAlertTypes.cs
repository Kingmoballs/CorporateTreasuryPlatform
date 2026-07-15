namespace Treasury.Shared.Constants;

public static class TreasuryAlertTypes
{
    public const string LowLiquidity = "LowLiquidity";

    public const string ForecastLiquidityGap =
        "ForecastLiquidityGap";

    public const string PendingApproval = "PendingApproval";

    public const string ReconciliationException =
        "ReconciliationException";

    public const string FxExposure = "FxExposure";

    public const string AuditException = "AuditException";

    public const string InvestmentMaturityUpcoming =
        "InvestmentMaturityUpcoming";

    public const string InvestmentMaturityOverdue =
        "InvestmentMaturityOverdue";

    public const string InvestmentConcentration =
        "InvestmentConcentration";

    public const string System = "System";
}