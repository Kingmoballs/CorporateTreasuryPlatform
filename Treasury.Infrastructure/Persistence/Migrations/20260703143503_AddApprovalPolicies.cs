using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalPolicies", x => x.Id);
                    table.CheckConstraint("CK_ApprovalPolicies_Currency", "char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_ApprovalPolicies_OperationType", "\"OperationType\" IN ('InternalTransfer', 'CashPayment')");
                    table.CheckConstraint("CK_ApprovalPolicies_Threshold", "\"ThresholdAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_ApprovalPolicies_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_OperationType_Currency",
                table: "ApprovalPolicies",
                columns: new[] { "OperationType", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_UpdatedByUserId",
                table: "ApprovalPolicies",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalPolicies");
        }
    }
}
