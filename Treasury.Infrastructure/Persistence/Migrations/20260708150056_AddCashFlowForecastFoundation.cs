using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashFlowForecastFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashFlowForecastItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    ExpectedDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CounterpartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RealizedTreasuryTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RealizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashFlowForecastItems", x => x.Id);
                    table.CheckConstraint("CK_CashFlowForecastItems_Amount_Positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_CashFlowForecastItems_Currency_Length", "char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_CashFlowForecastItems_Direction", "\"Direction\" IN ('Inflow', 'Outflow')");
                    table.CheckConstraint("CK_CashFlowForecastItems_SourceType", "\"SourceType\" IN ('Manual', 'CustomerReceipt', 'SupplierPayment', 'Payroll', 'Tax', 'Loan', 'Investment', 'Other')");
                    table.CheckConstraint("CK_CashFlowForecastItems_Status", "\"Status\" IN ('Active', 'Cancelled', 'Realized')");
                    table.ForeignKey(
                        name: "FK_CashFlowForecastItems_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashFlowForecastItems_TreasuryTransactions_RealizedTreasury~",
                        column: x => x.RealizedTreasuryTransactionId,
                        principalTable: "TreasuryTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashFlowForecastItems_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashFlowForecastItems_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastItems_AccountId_Status_ExpectedDateUtc",
                table: "CashFlowForecastItems",
                columns: new[] { "AccountId", "Status", "ExpectedDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastItems_CancelledByUserId",
                table: "CashFlowForecastItems",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastItems_CreatedByUserId",
                table: "CashFlowForecastItems",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastItems_Currency_Status_ExpectedDateUtc",
                table: "CashFlowForecastItems",
                columns: new[] { "Currency", "Status", "ExpectedDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastItems_RealizedTreasuryTransactionId",
                table: "CashFlowForecastItems",
                column: "RealizedTreasuryTransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashFlowForecastItems");
        }
    }
}
