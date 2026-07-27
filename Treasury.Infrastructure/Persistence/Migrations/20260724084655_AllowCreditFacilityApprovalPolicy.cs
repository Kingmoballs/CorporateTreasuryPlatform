using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowCreditFacilityApprovalPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies",
                sql: "\"OperationType\" IN ('InternalTransfer', 'CashPayment', 'TransactionReversal', 'InvestmentPlacement', 'InvestmentEarlyRedemption', 'InvestmentRollover', 'CreditFacilityActivation')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies",
                sql: "\"OperationType\" IN ('InternalTransfer', 'CashPayment', 'TransactionReversal', 'InvestmentPlacement', 'InvestmentEarlyRedemption', 'InvestmentRollover')");
        }
    }
}
