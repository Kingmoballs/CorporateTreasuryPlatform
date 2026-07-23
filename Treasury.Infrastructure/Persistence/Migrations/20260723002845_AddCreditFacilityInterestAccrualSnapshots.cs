using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditFacilityInterestAccrualSnapshots : Migration
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
                name: "CreditFacilityInterestAccrualSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditFacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FacilityReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FacilityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LenderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    FacilityStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OutstandingPrincipalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualInterestRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    DayCountBasis = table.Column<int>(type: "integer", nullable: false),
                    AccruedDays = table.Column<int>(type: "integer", nullable: false),
                    AccruedInterestBefore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedInterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedInterestAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditFacilityInterestAccrualSnapshots", x => x.Id);
                    table.CheckConstraint("CK_CreditFacilityInterestAccrualSnapshots_AccruedDays", "\"AccruedDays\" BETWEEN 1 AND 366");
                    table.CheckConstraint("CK_CreditFacilityInterestAccrualSnapshots_Amount_NonNegative", "\"AccruedInterestAmount\" >= 0");
                    table.CheckConstraint("CK_CreditFacilityInterestAccrualSnapshots_Currency", "char_length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")");
                    table.CheckConstraint("CK_CreditFacilityInterestAccrualSnapshots_DayCountBasis", "\"DayCountBasis\" IN (360, 365)");
                    table.CheckConstraint("CK_CreditFacilityInterestAccrualSnapshots_InterestAfter_NonNeg~", "\"AccruedInterestAfter\" >= 0");
                    table.CheckConstraint("CK_CreditFacilityInterestAccrualSnapshots_InterestBefore_NonNe~", "\"AccruedInterestBefore\" >= 0");
                    table.CheckConstraint("CK_CreditFacilityInterestAccrualSnapshots_InterestMovement", "\"AccruedInterestAfter\" = \"AccruedInterestBefore\" + \"AccruedInterestAmount\"");
                    table.CheckConstraint("CK_CreditFacilityInterestAccrualSnapshots_Principal_NonNegative", "\"OutstandingPrincipalAmount\" >= 0");
                    table.CheckConstraint("CK_CreditFacilityInterestAccrualSnapshots_Rate_Range", "\"AnnualInterestRate\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_CreditFacilityInterestAccrualSnapshots_CreditFacilities_Cre~",
                        column: x => x.CreditFacilityId,
                        principalTable: "CreditFacilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditFacilityInterestAccrualSnapshots_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Activated','Matured','Redeemed','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged','Suspended','Closed','DrawnDown','Repaid','Accrued')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','CreditFacilityDrawdown','CreditFacilityRepayment','CreditFacilityInterestAccrualSnapshot','System')");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityInterestAccrualSnapshots_CreatedByUserId",
                table: "CreditFacilityInterestAccrualSnapshots",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityInterestAccrualSnapshots_CreditFacilityId_Sna~",
                table: "CreditFacilityInterestAccrualSnapshots",
                columns: new[] { "CreditFacilityId", "SnapshotDateUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityInterestAccrualSnapshots_Currency_SnapshotDat~",
                table: "CreditFacilityInterestAccrualSnapshots",
                columns: new[] { "Currency", "SnapshotDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditFacilityInterestAccrualSnapshots_SnapshotDateUtc",
                table: "CreditFacilityInterestAccrualSnapshots",
                column: "SnapshotDateUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditFacilityInterestAccrualSnapshots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Activated','Matured','Redeemed','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged','Suspended','Closed','DrawnDown','Repaid')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','CreditFacilityDrawdown','CreditFacilityRepayment','System')");
        }
    }
}
