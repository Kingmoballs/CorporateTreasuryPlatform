using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentAccrualSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvestmentAccrualSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentPlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotDateUtc = table.Column<DateTime>(type: "date", nullable: false),
                    InvestmentReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InstitutionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PlacementStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualInterestRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    DayCountBasis = table.Column<int>(type: "integer", nullable: false),
                    AccruedDays = table.Column<int>(type: "integer", nullable: false),
                    ExpectedInterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedInterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CarryingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsOutstandingAsOf = table.Column<bool>(type: "boolean", nullable: false),
                    IsRedeemedAsOf = table.Column<bool>(type: "boolean", nullable: false),
                    ActualInterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RealizedNetInterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualRedemptionProceeds = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestVarianceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RealizedAnnualizedYieldPercentage = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentAccrualSnapshots", x => x.Id);
                    table.CheckConstraint("CK_InvestmentAccrualSnapshots_AccruedDays", "\"AccruedDays\" >= 0");
                    table.CheckConstraint("CK_InvestmentAccrualSnapshots_Amounts", "\"ExpectedInterestAmount\" >= 0 AND \"AccruedInterestAmount\" >= 0 AND \"CarryingAmount\" >= 0 AND \"ActualInterestAmount\" >= 0 AND \"WithholdingTaxAmount\" >= 0 AND \"RealizedNetInterestAmount\" >= 0 AND \"ActualRedemptionProceeds\" >= 0");
                    table.CheckConstraint("CK_InvestmentAccrualSnapshots_Currency", "char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_InvestmentAccrualSnapshots_DayCountBasis", "\"DayCountBasis\" IN (360, 365)");
                    table.CheckConstraint("CK_InvestmentAccrualSnapshots_PositionState", "(\"IsOutstandingAsOf\" AND NOT \"IsRedeemedAsOf\") OR (NOT \"IsOutstandingAsOf\" AND \"IsRedeemedAsOf\")");
                    table.CheckConstraint("CK_InvestmentAccrualSnapshots_Principal", "\"PrincipalAmount\" > 0");
                    table.CheckConstraint("CK_InvestmentAccrualSnapshots_Rate", "\"AnnualInterestRate\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_InvestmentAccrualSnapshots_Status", "\"PlacementStatus\" IN ('Active', 'Matured', 'Redeemed')");
                    table.ForeignKey(
                        name: "FK_InvestmentAccrualSnapshots_InvestmentPlacements_InvestmentP~",
                        column: x => x.InvestmentPlacementId,
                        principalTable: "InvestmentPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentAccrualSnapshots_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentAccrualSnapshots_CreatedByUserId",
                table: "InvestmentAccrualSnapshots",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentAccrualSnapshots_InstitutionName",
                table: "InvestmentAccrualSnapshots",
                column: "InstitutionName");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentAccrualSnapshots_InvestmentPlacementId_SnapshotDa~",
                table: "InvestmentAccrualSnapshots",
                columns: new[] { "InvestmentPlacementId", "SnapshotDateUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentAccrualSnapshots_SnapshotDateUtc_Currency",
                table: "InvestmentAccrualSnapshots",
                columns: new[] { "SnapshotDateUtc", "Currency" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvestmentAccrualSnapshots");
        }
    }
}
