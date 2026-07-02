using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalCashMovementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "TreasuryTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterpartyName",
                table: "TreasuryTransactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                table: "TreasuryTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "TreasuryTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_ExternalReference",
                table: "TreasuryTransactions",
                column: "ExternalReference");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_IdempotencyKey",
                table: "TreasuryTransactions",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TreasuryTransactions_ExternalReference",
                table: "TreasuryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TreasuryTransactions_IdempotencyKey",
                table: "TreasuryTransactions");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "TreasuryTransactions");

            migrationBuilder.DropColumn(
                name: "CounterpartyName",
                table: "TreasuryTransactions");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                table: "TreasuryTransactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "TreasuryTransactions");
        }
    }
}
