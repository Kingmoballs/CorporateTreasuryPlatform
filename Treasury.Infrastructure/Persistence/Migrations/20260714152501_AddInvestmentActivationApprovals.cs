using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentActivationApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InvestmentPlacements_Status",
                table: "InvestmentPlacements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies");

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivationExpiresAtUtc",
                table: "InvestmentPlacements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivationRejectedAtUtc",
                table: "InvestmentPlacements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActivationRejectedByUserId",
                table: "InvestmentPlacements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivationRejectionReason",
                table: "InvestmentPlacements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivationRequestedAtUtc",
                table: "InvestmentPlacements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActivationRequestedByUserId",
                table: "InvestmentPlacements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalCount",
                table: "InvestmentPlacements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovalCount",
                table: "InvestmentPlacements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "InvestmentPlacementId",
                table: "ApprovalDecisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_ActivationRejectedByUserId",
                table: "InvestmentPlacements",
                column: "ActivationRejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_ActivationRequestedByUserId",
                table: "InvestmentPlacements",
                column: "ActivationRequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_Status_ActivationExpiresAtUtc",
                table: "InvestmentPlacements",
                columns: new[] { "Status", "ActivationExpiresAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvestmentPlacements_ApprovalCounts",
                table: "InvestmentPlacements",
                sql: "\"RequiredApprovalCount\" BETWEEN 0 AND 5 AND \"ApprovalCount\" >= 0 AND \"ApprovalCount\" <= \"RequiredApprovalCount\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvestmentPlacements_Status",
                table: "InvestmentPlacements",
                sql: "\"Status\" IN ('Draft', 'PendingActivation', 'Active', 'Matured', 'Redeemed', 'ActivationRejected', 'ActivationExpired', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies",
                sql: "\"OperationType\" IN ('InternalTransfer', 'CashPayment', 'TransactionReversal', 'InvestmentPlacement')");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_InvestmentPlacementId_ApproverUserId",
                table: "ApprovalDecisions",
                columns: new[] { "InvestmentPlacementId", "ApproverUserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_InvestmentPlacements_InvestmentPlacementId",
                table: "ApprovalDecisions",
                column: "InvestmentPlacementId",
                principalTable: "InvestmentPlacements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentPlacements_Users_ActivationRejectedByUserId",
                table: "InvestmentPlacements",
                column: "ActivationRejectedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentPlacements_Users_ActivationRequestedByUserId",
                table: "InvestmentPlacements",
                column: "ActivationRequestedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_InvestmentPlacements_InvestmentPlacementId",
                table: "ApprovalDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentPlacements_Users_ActivationRejectedByUserId",
                table: "InvestmentPlacements");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentPlacements_Users_ActivationRequestedByUserId",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_ActivationRejectedByUserId",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_ActivationRequestedByUserId",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_Status_ActivationExpiresAtUtc",
                table: "InvestmentPlacements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvestmentPlacements_ApprovalCounts",
                table: "InvestmentPlacements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvestmentPlacements_Status",
                table: "InvestmentPlacements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalDecisions_InvestmentPlacementId_ApproverUserId",
                table: "ApprovalDecisions");

            migrationBuilder.DropColumn(
                name: "ActivationExpiresAtUtc",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "ActivationRejectedAtUtc",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "ActivationRejectedByUserId",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "ActivationRejectionReason",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "ActivationRequestedAtUtc",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "ActivationRequestedByUserId",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "ApprovalCount",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "RequiredApprovalCount",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "InvestmentPlacementId",
                table: "ApprovalDecisions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvestmentPlacements_Status",
                table: "InvestmentPlacements",
                sql: "\"Status\" IN ('Draft', 'Active', 'Matured', 'Redeemed', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalPolicies_OperationType",
                table: "ApprovalPolicies",
                sql: "\"OperationType\" IN ('InternalTransfer', 'CashPayment', 'TransactionReversal')");
        }
    }
}
