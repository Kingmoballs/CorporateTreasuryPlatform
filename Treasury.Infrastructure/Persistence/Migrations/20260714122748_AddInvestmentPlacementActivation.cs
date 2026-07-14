using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentPlacementActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAtUtc",
                table: "InvestmentPlacements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActivatedByUserId",
                table: "InvestmentPlacements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivationIdempotencyKey",
                table: "InvestmentPlacements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FundingTreasuryTransactionId",
                table: "InvestmentPlacements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaturityForecastItemId",
                table: "InvestmentPlacements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_ActivatedByUserId",
                table: "InvestmentPlacements",
                column: "ActivatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_ActivationIdempotencyKey",
                table: "InvestmentPlacements",
                column: "ActivationIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_FundingTreasuryTransactionId",
                table: "InvestmentPlacements",
                column: "FundingTreasuryTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_MaturityForecastItemId",
                table: "InvestmentPlacements",
                column: "MaturityForecastItemId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Activated','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged')");

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentPlacements_CashFlowForecastItems_MaturityForecast~",
                table: "InvestmentPlacements",
                column: "MaturityForecastItemId",
                principalTable: "CashFlowForecastItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentPlacements_TreasuryTransactions_FundingTreasuryTr~",
                table: "InvestmentPlacements",
                column: "FundingTreasuryTransactionId",
                principalTable: "TreasuryTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentPlacements_Users_ActivatedByUserId",
                table: "InvestmentPlacements",
                column: "ActivatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentPlacements_CashFlowForecastItems_MaturityForecast~",
                table: "InvestmentPlacements");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentPlacements_TreasuryTransactions_FundingTreasuryTr~",
                table: "InvestmentPlacements");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentPlacements_Users_ActivatedByUserId",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_ActivatedByUserId",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_ActivationIdempotencyKey",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_FundingTreasuryTransactionId",
                table: "InvestmentPlacements");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_MaturityForecastItemId",
                table: "InvestmentPlacements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "ActivatedByUserId",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "ActivationIdempotencyKey",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "FundingTreasuryTransactionId",
                table: "InvestmentPlacements");

            migrationBuilder.DropColumn(
                name: "MaturityForecastItemId",
                table: "InvestmentPlacements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged')");
        }
    }
}
