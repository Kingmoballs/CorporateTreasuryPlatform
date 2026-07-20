using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCounterpartyInvestmentLimitFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.AddColumn<Guid>(
                name: "CounterpartyId",
                table: "InvestmentPlacements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Counterparties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CounterpartyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    SwiftCode = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    CreditRating = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counterparties", x => x.Id);
                    table.CheckConstraint("CK_Counterparties_Code", "\"Code\" ~ '^[A-Z0-9][A-Z0-9-]{0,29}$'");
                    table.CheckConstraint("CK_Counterparties_CountryCode", "\"CountryCode\" ~ '^[A-Z]{2}$'");
                    table.CheckConstraint("CK_Counterparties_Name", "char_length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("CK_Counterparties_SwiftCode", "\"SwiftCode\" IS NULL OR \"SwiftCode\" ~ '^[A-Z0-9]{8}([A-Z0-9]{3})?$'");
                    table.CheckConstraint("CK_Counterparties_Type", "\"CounterpartyType\" IN ('Bank','NonBankFinancialInstitution','Corporate','Government')");
                    table.ForeignKey(
                        name: "FK_Counterparties_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Counterparties_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvestmentLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CounterpartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    InvestmentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaximumExposureAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WarningThresholdPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 80m),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentLimits", x => x.Id);
                    table.CheckConstraint("CK_InvestmentLimits_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_InvestmentLimits_EffectiveDates", "\"EffectiveToUtc\" IS NULL OR \"EffectiveToUtc\" > \"EffectiveFromUtc\"");
                    table.CheckConstraint("CK_InvestmentLimits_InvestmentType", "\"InvestmentType\" IN ('All','FixedDeposit')");
                    table.CheckConstraint("CK_InvestmentLimits_MaximumExposure", "\"MaximumExposureAmount\" > 0");
                    table.CheckConstraint("CK_InvestmentLimits_WarningThreshold", "\"WarningThresholdPercentage\" > 0 AND \"WarningThresholdPercentage\" <= 100");
                    table.ForeignKey(
                        name: "FK_InvestmentLimits_Counterparties_CounterpartyId",
                        column: x => x.CounterpartyId,
                        principalTable: "Counterparties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentLimits_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentLimits_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlacements_CounterpartyId",
                table: "InvestmentPlacements",
                column: "CounterpartyId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentEarlyRedemptionRequest','InvestmentRolloverRequest','Counterparty','InvestmentLimit','System')");

            migrationBuilder.CreateIndex(
                name: "IX_Counterparties_Code",
                table: "Counterparties",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Counterparties_CreatedByUserId",
                table: "Counterparties",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Counterparties_IsActive_Name",
                table: "Counterparties",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Counterparties_UpdatedByUserId",
                table: "Counterparties",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentLimits_CounterpartyId_Currency_InvestmentType_Eff~",
                table: "InvestmentLimits",
                columns: new[] { "CounterpartyId", "Currency", "InvestmentType", "EffectiveFromUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentLimits_CreatedByUserId",
                table: "InvestmentLimits",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentLimits_IsActive_EffectiveFromUtc_EffectiveToUtc",
                table: "InvestmentLimits",
                columns: new[] { "IsActive", "EffectiveFromUtc", "EffectiveToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentLimits_UpdatedByUserId",
                table: "InvestmentLimits",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentPlacements_Counterparties_CounterpartyId",
                table: "InvestmentPlacements",
                column: "CounterpartyId",
                principalTable: "Counterparties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentPlacements_Counterparties_CounterpartyId",
                table: "InvestmentPlacements");

            migrationBuilder.DropTable(
                name: "InvestmentLimits");

            migrationBuilder.DropTable(
                name: "Counterparties");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentPlacements_CounterpartyId",
                table: "InvestmentPlacements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CounterpartyId",
                table: "InvestmentPlacements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentEarlyRedemptionRequest','InvestmentRolloverRequest','System')");
        }
    }
}
