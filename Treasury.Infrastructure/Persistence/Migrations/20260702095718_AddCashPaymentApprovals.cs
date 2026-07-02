using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashPaymentApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PaymentRequestId",
                table: "TreasuryTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    BeneficiaryName = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    ExternalReference = table.Column<string>(type: "text", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_PaymentRequests", x => x.Id);
                    table.CheckConstraint("CK_PaymentRequests_Amount_Positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_PaymentRequests_Status", "\"Status\" IN ('Pending', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_PaymentRequests_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_PaymentRequestId",
                table: "TreasuryTransactions",
                column: "PaymentRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_AccountId",
                table: "PaymentRequests",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_IdempotencyKey",
                table: "PaymentRequests",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_RequestedByUserId",
                table: "PaymentRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_ReviewedByUserId",
                table: "PaymentRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_Status",
                table: "PaymentRequests",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_TreasuryTransactions_PaymentRequests_PaymentRequestId",
                table: "TreasuryTransactions",
                column: "PaymentRequestId",
                principalTable: "PaymentRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TreasuryTransactions_PaymentRequests_PaymentRequestId",
                table: "TreasuryTransactions");

            migrationBuilder.DropTable(
                name: "PaymentRequests");

            migrationBuilder.DropIndex(
                name: "IX_TreasuryTransactions_PaymentRequestId",
                table: "TreasuryTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentRequestId",
                table: "TreasuryTransactions");
        }
    }
}
