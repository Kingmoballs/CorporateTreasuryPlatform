using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenTransferApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "TransferRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "TransferRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByUserId",
                table: "TransferRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "TransferRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "TransferRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_CreatedAt",
                table: "TransferRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_Status",
                table: "TransferRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_CreatedAt",
                table: "TransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_Status",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "RequestedByUserId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "TransferRequests");
        }
    }
}
