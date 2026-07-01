using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_TreasuryTransactions_Amount_Positive",
                table: "TreasuryTransactions",
                sql: "\"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TreasuryTransactions_Currency_Length",
                table: "TreasuryTransactions",
                sql: "char_length(\"Currency\") = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TreasuryTransactions_Status",
                table: "TreasuryTransactions",
                sql: "\"Status\" IN ('Completed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransferRequests_Amount_Positive",
                table: "TransferRequests",
                sql: "\"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransferRequests_Status",
                table: "TransferRequests",
                sql: "\"Status\" IN ('Pending', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_Amount_Positive",
                table: "LedgerEntries",
                sql: "\"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_EntryType",
                table: "LedgerEntries",
                sql: "\"EntryType\" IN ('Debit', 'Credit')");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTypes_Name",
                table: "AccountTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountNumber",
                table: "Accounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Balance_NonNegative",
                table: "Accounts",
                sql: "\"Balance\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Currency_Length",
                table: "Accounts",
                sql: "char_length(\"Currency\") = 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TreasuryTransactions_Amount_Positive",
                table: "TreasuryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TreasuryTransactions_Currency_Length",
                table: "TreasuryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TreasuryTransactions_Status",
                table: "TreasuryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransferRequests_Amount_Positive",
                table: "TransferRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransferRequests_Status",
                table: "TransferRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_Amount_Positive",
                table: "LedgerEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_EntryType",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_AccountTypes_Name",
                table: "AccountTypes");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_AccountNumber",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Balance_NonNegative",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Currency_Length",
                table: "Accounts");
        }
    }
}
