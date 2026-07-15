using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentTreasuryAlertTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TreasuryAlerts_AlertType",
                table: "TreasuryAlerts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TreasuryAlerts_AlertType",
                table: "TreasuryAlerts",
                sql: "\"AlertType\" IN ('LowLiquidity', 'ForecastLiquidityGap', 'PendingApproval', 'ReconciliationException', 'FxExposure', 'AuditException', 'InvestmentMaturityUpcoming', 'InvestmentMaturityOverdue', 'InvestmentConcentration', 'System')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TreasuryAlerts_AlertType",
                table: "TreasuryAlerts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TreasuryAlerts_AlertType",
                table: "TreasuryAlerts",
                sql: "\"AlertType\" IN ('LowLiquidity', 'ForecastLiquidityGap', 'PendingApproval', 'ReconciliationException', 'FxExposure', 'AuditException', 'System')");
        }
    }
}
