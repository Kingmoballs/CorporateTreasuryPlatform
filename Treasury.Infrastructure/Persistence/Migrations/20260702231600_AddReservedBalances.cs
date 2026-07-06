using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservedBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReservedBalance",
                table: "Accounts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_ReservedBalance_NonNegative",
                table: "Accounts",
                sql: "\"ReservedBalance\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_ReservedBalance_NotAboveBalance",
                table: "Accounts",
                sql: "\"ReservedBalance\" <= \"Balance\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_ReservedBalance_NonNegative",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_ReservedBalance_NotAboveBalance",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "ReservedBalance",
                table: "Accounts");
        }
    }
}
