using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditFacilityRepayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.CreateTable(
                name: "CreditFacilityRepayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreditFacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OutstandingPrincipalBefore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OutstandingPrincipalAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedInterestBefore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedInterestAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Completed"),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TreasuryTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepaymentDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditFacilityRepayments", x => x.Id);
                    table.CheckConstraint("CK_CreditFacilityRepayments_AccruedInterestAfter_NonNegative", "\"AccruedInterestAfter\" >= 0");
                    table.CheckConstraint("CK_CreditFacilityRepayments_AccruedInterestBefore_NonNegative", "\"AccruedInterestBefore\" >= 0");
                    table.CheckConstraint("CK_CreditFacilityRepayments_Allocation", "\"Amount\" = \"PrincipalAmount\" + \"InterestAmount\"");
                    table.CheckConstraint("CK_CreditFacilityRepayments_Amount_Positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_CreditFacilityRepayments_Currency", "char_length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")");
                    table.CheckConstraint("CK_CreditFacilityRepayments_Interest_NonNegative", "\"InterestAmount\" >= 0");
                    table.CheckConstraint("CK_CreditFacilityRepayments_InterestMovement", "\"AccruedInterestAfter\" = \"AccruedInterestBefore\" - \"InterestAmount\"");
                    table.CheckConstraint("CK_CreditFacilityRepayments_Principal_NonNegative", "\"PrincipalAmount\" >= 0");
                    table.CheckConstraint("CK_CreditFacilityRepayments_PrincipalAfter_NonNegative", "\"OutstandingPrincipalAfter\" >= 0");
                    table.CheckConstraint("CK_CreditFacilityRepayments_PrincipalBefore_NonNegative", "\"OutstandingPrincipalBefore\" >= 0");
                    table.CheckConstraint("CK_CreditFacilityRepayments_PrincipalMovement", "\"OutstandingPrincipalAfter\" = \"OutstandingPrincipalBefore\" - \"PrincipalAmount\"");
                    table.CheckConstraint("CK_CreditFacilityRepayments_Status", "\"Status\" IN ('Completed')");
                    table.ForeignKey(
                        name: "FK_CreditFacilityRepayments_Accounts_SettlementAccountId",
                        column: x => x.SettlementAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilityRepayments_CreditFacilities_CreditFacilityId",
                        column: x => x.CreditFacilityId,
                        principalTable: "CreditFacilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilityRepayments_TreasuryTransactions_TreasuryTrans~",
                        column: x => x.TreasuryTransactionId,
                        principalTable: "TreasuryTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilityRepayments_Users_InitiatedByUserId",
                        column: x => x.InitiatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Activated','Matured','Redeemed','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged','Suspended','Closed','DrawnDown','Repaid')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','CreditFacilityDrawdown','CreditFacilityRepayment','System')");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityRepayments_CreditFacilityId_RepaymentDateUtc",
                table: "CreditFacilityRepayments",
                columns: new[] { "CreditFacilityId", "RepaymentDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityRepayments_IdempotencyKey",
                table: "CreditFacilityRepayments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityRepayments_InitiatedByUserId",
                table: "CreditFacilityRepayments",
                column: "InitiatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityRepayments_Reference",
                table: "CreditFacilityRepayments",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityRepayments_SettlementAccountId_RepaymentDateU~",
                table: "CreditFacilityRepayments",
                columns: new[] { "SettlementAccountId", "RepaymentDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityRepayments_TreasuryTransactionId",
                table: "CreditFacilityRepayments",
                column: "TreasuryTransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditFacilityRepayments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Activated','Matured','Redeemed','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged','Suspended','Closed','DrawnDown')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','CreditFacilityDrawdown','System')");
        }
    }
}
