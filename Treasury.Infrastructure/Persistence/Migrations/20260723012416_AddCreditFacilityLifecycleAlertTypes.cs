using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditFacilityLifecycleAlertTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TreasuryAlerts_AlertType",
                table: "TreasuryAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TreasuryAlerts_AlertType",
                table: "TreasuryAlerts",
                sql: "\"AlertType\" IN ('LowLiquidity', 'ForecastLiquidityGap', 'PendingApproval', 'ReconciliationException', 'FxExposure', 'AuditException', 'InvestmentMaturityUpcoming', 'InvestmentMaturityOverdue', 'InvestmentConcentration', 'InvestmentLimitWarning', 'InvestmentLimitBreach', 'CreditFacilityDebtOverdue', 'System')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Activated','Matured','Redeemed','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged','Suspended','Closed','DrawnDown','Repaid','Accrued','Reactivated')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TreasuryAlerts_AlertType",
                table: "TreasuryAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TreasuryAlerts_AlertType",
                table: "TreasuryAlerts",
                sql: "\"AlertType\" IN ('LowLiquidity', 'ForecastLiquidityGap', 'PendingApproval', 'ReconciliationException', 'FxExposure', 'AuditException', 'InvestmentMaturityUpcoming', 'InvestmentMaturityOverdue', 'InvestmentConcentration', 'InvestmentLimitWarning', 'InvestmentLimitBreach', 'System')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Activated','Matured','Redeemed','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged','Suspended','Closed','DrawnDown','Repaid','Accrued')");
        }
    }
}
