using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentMaturityAndRedemptionCorrected : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualInterestAmount",
                table: "InvestmentPlacements",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualMaturityAmount",
                table: "InvestmentPlacements",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "RedeemedAtUtc",
                table: "InvestmentPlacements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RedeemedByUserId",
                table: "InvestmentPlacements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RedemptionAccountId",
                table: "InvestmentPlacements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedemptionExternalReference",
                table: "InvestmentPlacements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedemptionIdempotencyKey",
                table: "InvestmentPlacements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedemptionNotes",
                table: "InvestmentPlacements",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RedemptionTreasuryTransactionId",
                table: "InvestmentPlacements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingTaxAmount",
                table: "InvestmentPlacements",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_RedeemedByUserId",
                table: "InvestmentPlacements",
                column: "RedeemedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_RedemptionAccountId",
                table: "InvestmentPlacements",
                column: "RedemptionAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_RedemptionIdempotencyKey",
                table: "InvestmentPlacements",
                column: "RedemptionIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_RedemptionTreasuryTransactionId",
                table: "InvestmentPlacements",
                column: "RedemptionTreasuryTransactionId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvestmentPlacements_ActualInterest_NonNegative",
                table: "InvestmentPlacements",
                sql: "\"ActualInterestAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvestmentPlacements_RedemptionAmount",
                table: "InvestmentPlacements",
                sql: "\"Status\" <> 'Redeemed' OR \"ActualMaturityAmount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvestmentPlacements_WithholdingTax_NonNegative",
                table: "InvestmentPlacements",
                sql: "\"WithholdingTaxAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvestmentPlacements_WithholdingTax_NotAboveInterest",
                table: "InvestmentPlacements",
                sql: "\"WithholdingTaxAmount\" <= \"ActualInterestAmount\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Activated','Matured','Redeemed','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged')");

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentPlacements_Accounts_RedemptionAccountId",
                table: "InvestmentPlacements",
                column: "RedemptionAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentPlacements_TreasuryTransactions_RedemptionTreasur~",
                table: "InvestmentPlacements",
                column: "RedemptionTreasuryTransactionId",
                principalTable: "TreasuryTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentPlacements_Users_RedeemedByUserId",
                table: "InvestmentPlacements",
                column: "RedeemedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentPlacements_Accounts_RedemptionAccountId",
                table: "InvestmentPlacements");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentPlacements_TreasuryTransactions_RedemptionTreasur~",
                table: "InvestmentPlacements");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentPlacements_Users_RedeemedByUserId",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_RedeemedByUserId",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_RedemptionAccountId",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_RedemptionIdempotencyKey",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_RedemptionTreasuryTransactionId",
                table: "InvestmentPlacements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvestmentPlacements_ActualInterest_NonNegative",
                table: "InvestmentPlacements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvestmentPlacements_RedemptionAmount",
                table: "InvestmentPlacements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvestmentPlacements_WithholdingTax_NonNegative",
                table: "InvestmentPlacements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvestmentPlacements_WithholdingTax_NotAboveInterest",
                table: "InvestmentPlacements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActualInterestAmount",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "ActualMaturityAmount",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "RedeemedAtUtc",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "RedeemedByUserId",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "RedemptionAccountId",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "RedemptionExternalReference",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "RedemptionIdempotencyKey",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "RedemptionNotes",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "RedemptionTreasuryTransactionId",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxAmount",
                table: "InvestmentPlacements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Activated','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged')");
        }
    }
}
