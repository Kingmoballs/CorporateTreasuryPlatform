using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingRequestExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TransferRequests_Status",
                table: "TransferRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReversalRequests_Status",
                table: "ReversalRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentRequests_Status",
                table: "PaymentRequests");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "TransferRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "ReversalRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "PaymentRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingRequestExpiryHours",
                table: "ApprovalPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_Status_ExpiresAtUtc",
                table: "TransferRequests",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransferRequests_Status",
                table: "TransferRequests",
                sql: "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Expired')");

            migrationBuilder.CreateIndex(
                name: "IX_ReversalRequests_Status_ExpiresAtUtc",
                table: "ReversalRequests",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReversalRequests_Status",
                table: "ReversalRequests",
                sql: "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Expired')");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_Status_ExpiresAtUtc",
                table: "PaymentRequests",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentRequests_Status",
                table: "PaymentRequests",
                sql: "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Expired')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalPolicies_PendingRequestExpiryHours",
                table: "ApprovalPolicies",
                sql: "\"PendingRequestExpiryHours\" BETWEEN 1 AND 168");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_Status_ExpiresAtUtc",
                table: "TransferRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransferRequests_Status",
                table: "TransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_ReversalRequests_Status_ExpiresAtUtc",
                table: "ReversalRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReversalRequests_Status",
                table: "ReversalRequests");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRequests_Status_ExpiresAtUtc",
                table: "PaymentRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentRequests_Status",
                table: "PaymentRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalPolicies_PendingRequestExpiryHours",
                table: "ApprovalPolicies");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "ReversalRequests");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "PendingRequestExpiryHours",
                table: "ApprovalPolicies");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransferRequests_Status",
                table: "TransferRequests",
                sql: "\"Status\" IN ('Pending', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReversalRequests_Status",
                table: "ReversalRequests",
                sql: "\"Status\" IN ('Pending', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentRequests_Status",
                table: "PaymentRequests",
                sql: "\"Status\" IN ('Pending', 'Approved', 'Rejected')");
        }
    }
}
