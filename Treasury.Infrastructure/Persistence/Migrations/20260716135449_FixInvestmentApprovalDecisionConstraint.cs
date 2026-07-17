using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixInvestmentApprovalDecisionConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalDecisions_OneRequest",
                table: "ApprovalDecisions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalDecisions_OneRequest",
                table: "ApprovalDecisions",
                sql: "num_nonnulls(\"TransferRequestId\", \"PaymentRequestId\", \"ReversalRequestId\", \"InvestmentPlacementId\") = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalDecisions_OneRequest",
                table: "ApprovalDecisions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalDecisions_OneRequest",
                table: "ApprovalDecisions",
                sql: "num_nonnulls(\"TransferRequestId\", \"PaymentRequestId\", \"ReversalRequestId\") = 1");
        }
    }
}
