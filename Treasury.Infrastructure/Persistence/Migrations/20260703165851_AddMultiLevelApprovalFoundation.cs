using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiLevelApprovalFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalCount",
                table: "TransferRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovalCount",
                table: "TransferRequests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalCount",
                table: "ReversalRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovalCount",
                table: "ReversalRequests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalCount",
                table: "PaymentRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovalCount",
                table: "PaymentRequests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovalCount",
                table: "ApprovalPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ApprovalDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDecisions", x => x.Id);
                    table.CheckConstraint("CK_ApprovalDecisions_Decision", "\"Decision\" IN ('Approved', 'Rejected')");
                    table.CheckConstraint("CK_ApprovalDecisions_OneRequest", "num_nonnulls(\"TransferRequestId\", \"PaymentRequestId\", \"ReversalRequestId\") = 1");
                    table.ForeignKey(
                        name: "FK_ApprovalDecisions_PaymentRequests_PaymentRequestId",
                        column: x => x.PaymentRequestId,
                        principalTable: "PaymentRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalDecisions_ReversalRequests_ReversalRequestId",
                        column: x => x.ReversalRequestId,
                        principalTable: "ReversalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalDecisions_TransferRequests_TransferRequestId",
                        column: x => x.TransferRequestId,
                        principalTable: "TransferRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalDecisions_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransferRequests_ApprovalCounts",
                table: "TransferRequests",
                sql: "\"RequiredApprovalCount\" >= 1 AND \"ApprovalCount\" >= 0 AND \"ApprovalCount\" <= \"RequiredApprovalCount\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReversalRequests_ApprovalCounts",
                table: "ReversalRequests",
                sql: "\"RequiredApprovalCount\" >= 1 AND \"ApprovalCount\" >= 0 AND \"ApprovalCount\" <= \"RequiredApprovalCount\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentRequests_ApprovalCounts",
                table: "PaymentRequests",
                sql: "\"RequiredApprovalCount\" >= 1 AND \"ApprovalCount\" >= 0 AND \"ApprovalCount\" <= \"RequiredApprovalCount\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalPolicies_RequiredApprovalCount",
                table: "ApprovalPolicies",
                sql: "\"RequiredApprovalCount\" BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_ApproverUserId",
                table: "ApprovalDecisions",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_PaymentRequestId_ApproverUserId",
                table: "ApprovalDecisions",
                columns: new[] { "PaymentRequestId", "ApproverUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_ReversalRequestId_ApproverUserId",
                table: "ApprovalDecisions",
                columns: new[] { "ReversalRequestId", "ApproverUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_TransferRequestId_ApproverUserId",
                table: "ApprovalDecisions",
                columns: new[] { "TransferRequestId", "ApproverUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransferRequests_ApprovalCounts",
                table: "TransferRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReversalRequests_ApprovalCounts",
                table: "ReversalRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentRequests_ApprovalCounts",
                table: "PaymentRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalPolicies_RequiredApprovalCount",
                table: "ApprovalPolicies");

            migrationBuilder.DropColumn(
                name: "ApprovalCount",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "RequiredApprovalCount",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalCount",
                table: "ReversalRequests");

            migrationBuilder.DropColumn(
                name: "RequiredApprovalCount",
                table: "ReversalRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalCount",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "RequiredApprovalCount",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "RequiredApprovalCount",
                table: "ApprovalPolicies");
        }
    }
}
