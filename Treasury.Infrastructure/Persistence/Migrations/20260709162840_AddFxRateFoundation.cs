using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFxRateFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FxRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ToCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric", nullable: false),
                    RateDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FxRates", x => x.Id);
                    table.CheckConstraint("CK_FxRates_DifferentCurrencies", "\"FromCurrency\" <> \"ToCurrency\"");
                    table.CheckConstraint("CK_FxRates_FromCurrency_Length", "char_length(\"FromCurrency\") = 3");
                    table.CheckConstraint("CK_FxRates_Rate_Positive", "\"Rate\" > 0");
                    table.CheckConstraint("CK_FxRates_SourceType", "\"SourceType\" IN ('Manual', 'CentralBank', 'Bank', 'Market', 'Other')");
                    table.CheckConstraint("CK_FxRates_ToCurrency_Length", "char_length(\"ToCurrency\") = 3");
                    table.ForeignKey(
                        name: "FK_FxRates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_CreatedByUserId",
                table: "FxRates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_FromCurrency_ToCurrency_RateDateUtc",
                table: "FxRates",
                columns: new[] { "FromCurrency", "ToCurrency", "RateDateUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_ToCurrency_RateDateUtc",
                table: "FxRates",
                columns: new[] { "ToCurrency", "RateDateUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FxRates");
        }
    }
}
