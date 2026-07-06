using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionReversals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReversalRequestId",
                table: "TreasuryTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesTransactionId",
                table: "TreasuryTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReversalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReversalRequests", x => x.Id);
                    table.CheckConstraint("CK_ReversalRequests_Status", "\"Status\" IN ('Pending', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_ReversalRequests_TreasuryTransactions_OriginalTransactionId",
                        column: x => x.OriginalTransactionId,
                        principalTable: "TreasuryTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReversalRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReversalRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_ReversalRequestId",
                table: "TreasuryTransactions",
                column: "ReversalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_ReversesTransactionId",
                table: "TreasuryTransactions",
                column: "ReversesTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReversalRequests_OriginalTransactionId",
                table: "ReversalRequests",
                column: "OriginalTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReversalRequests_RequestedByUserId",
                table: "ReversalRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReversalRequests_ReviewedByUserId",
                table: "ReversalRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReversalRequests_Status",
                table: "ReversalRequests",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_TreasuryTransactions_ReversalRequests_ReversalRequestId",
                table: "TreasuryTransactions",
                column: "ReversalRequestId",
                principalTable: "ReversalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TreasuryTransactions_TreasuryTransactions_ReversesTransacti~",
                table: "TreasuryTransactions",
                column: "ReversesTransactionId",
                principalTable: "TreasuryTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TreasuryTransactions_ReversalRequests_ReversalRequestId",
                table: "TreasuryTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_TreasuryTransactions_TreasuryTransactions_ReversesTransacti~",
                table: "TreasuryTransactions");

            migrationBuilder.DropTable(
                name: "ReversalRequests");

            migrationBuilder.DropIndex(
                name: "IX_TreasuryTransactions_ReversalRequestId",
                table: "TreasuryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TreasuryTransactions_ReversesTransactionId",
                table: "TreasuryTransactions");

            migrationBuilder.DropColumn(
                name: "ReversalRequestId",
                table: "TreasuryTransactions");

            migrationBuilder.DropColumn(
                name: "ReversesTransactionId",
                table: "TreasuryTransactions");
        }
    }
}
