using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTreasuryTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TreasuryTransactionId",
                table: "LedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TreasuryTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    TransactionType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SourceAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreasuryTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreasuryTransactions_Accounts_DestinationAccountId",
                        column: x => x.DestinationAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreasuryTransactions_Accounts_SourceAccountId",
                        column: x => x.SourceAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreasuryTransactions_TransferRequests_TransferRequestId",
                        column: x => x.TransferRequestId,
                        principalTable: "TransferRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreasuryTransactions_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreasuryTransactions_Users_InitiatedByUserId",
                        column: x => x.InitiatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_TreasuryTransactionId",
                table: "LedgerEntries",
                column: "TreasuryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_CompletedByUserId",
                table: "TreasuryTransactions",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_CreatedAtUtc",
                table: "TreasuryTransactions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_DestinationAccountId",
                table: "TreasuryTransactions",
                column: "DestinationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_InitiatedByUserId",
                table: "TreasuryTransactions",
                column: "InitiatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_Reference",
                table: "TreasuryTransactions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_SourceAccountId",
                table: "TreasuryTransactions",
                column: "SourceAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_TransferRequestId",
                table: "TreasuryTransactions",
                column: "TransferRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_TreasuryTransactions_TreasuryTransactionId",
                table: "LedgerEntries",
                column: "TreasuryTransactionId",
                principalTable: "TreasuryTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LedgerEntries_TreasuryTransactions_TreasuryTransactionId",
                table: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "TreasuryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_TreasuryTransactionId",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "TreasuryTransactionId",
                table: "LedgerEntries");
        }
    }
}
