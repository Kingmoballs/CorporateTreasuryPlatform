using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentPlacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.CreateTable(
                name: "InvestmentPlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InvestmentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InstitutionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AnnualInterestRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    DayCountBasis = table.Column<int>(type: "integer", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaturityDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedInterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedMaturityAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentPlacements", x => x.Id);
                    table.CheckConstraint("CK_InvestmentPlacements_Currency_Length", "char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_InvestmentPlacements_DayCountBasis", "\"DayCountBasis\" IN (360, 365)");
                    table.CheckConstraint("CK_InvestmentPlacements_InterestRate", "\"AnnualInterestRate\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_InvestmentPlacements_InvestmentType", "\"InvestmentType\" IN ('FixedDeposit')");
                    table.CheckConstraint("CK_InvestmentPlacements_MaturityDate", "\"MaturityDateUtc\" > \"StartDateUtc\"");
                    table.CheckConstraint("CK_InvestmentPlacements_PrincipalAmount_Positive", "\"PrincipalAmount\" > 0");
                    table.CheckConstraint("CK_InvestmentPlacements_Status", "\"Status\" IN ('Draft', 'Active', 'Matured', 'Redeemed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_InvestmentPlacements_Accounts_SourceAccountId",
                        column: x => x.SourceAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentPlacements_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentPlacements_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','System')");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_CancelledByUserId",
                table: "InvestmentPlacements",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_CreatedByUserId",
                table: "InvestmentPlacements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_Reference",
                table: "InvestmentPlacements",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_SourceAccountId",
                table: "InvestmentPlacements",
                column: "SourceAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_Status_MaturityDateUtc",
                table: "InvestmentPlacements",
                columns: new[] { "Status", "MaturityDateUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvestmentPlacements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','System')");
        }
    }
}
