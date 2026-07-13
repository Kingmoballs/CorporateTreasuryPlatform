using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTreasuryAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Users_ActorUserId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "UserAgent",
                table: "AuditLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                table: "AuditLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogs\" " +
                "ALTER COLUMN \"MetadataJson\" TYPE jsonb " +
                "USING NULLIF(\"MetadataJson\", '')::jsonb;");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "EntityReference",
                table: "AuditLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogs\" " +
                "ALTER COLUMN \"BeforeValuesJson\" TYPE jsonb " +
                "USING NULLIF(\"BeforeValuesJson\", '')::jsonb;");

            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogs\" " +
                "ALTER COLUMN \"AfterValuesJson\" TYPE jsonb " +
                "USING NULLIF(\"AfterValuesJson\", '')::jsonb;");

            migrationBuilder.AlterColumn<string>(
                name: "ActorRole",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActorEmail",
                table: "AuditLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "TreasuryAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    SourceModule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosureNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreasuryAlerts", x => x.Id);
                    table.CheckConstraint("CK_TreasuryAlerts_AlertType", "\"AlertType\" IN ('LowLiquidity', 'ForecastLiquidityGap', 'PendingApproval', 'ReconciliationException', 'FxExposure', 'AuditException', 'System')");
                    table.CheckConstraint("CK_TreasuryAlerts_Currency_Length", "\"Currency\" IS NULL OR char_length(\"Currency\") = 3");
                    table.CheckConstraint("CK_TreasuryAlerts_Severity", "\"Severity\" IN ('Info', 'Warning', 'Critical')");
                    table.CheckConstraint("CK_TreasuryAlerts_Status", "\"Status\" IN ('Open', 'Resolved', 'Dismissed')");
                    table.ForeignKey(
                        name: "FK_TreasuryAlerts_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreasuryAlerts_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreasuryAlerts_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId_OccurredAtUtc",
                table: "AuditLogs",
                columns: new[] { "ActorUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAtUtc",
                table: "AuditLogs",
                column: "OccurredAtUtc");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "\"Action\" IN ('Created','Updated','Deleted','Approved','Rejected','Resolved','Dismissed','Cancelled','Realized','Matched','Reconciled','Ignored','Expired','Imported','LoggedIn','RoleChanged')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','System')");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryAlerts_AccountId",
                table: "TreasuryAlerts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryAlerts_AlertType_Status",
                table: "TreasuryAlerts",
                columns: new[] { "AlertType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryAlerts_ClosedByUserId",
                table: "TreasuryAlerts",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryAlerts_CreatedAtUtc",
                table: "TreasuryAlerts",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryAlerts_CreatedByUserId",
                table: "TreasuryAlerts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryAlerts_SourceEntityType_SourceEntityId",
                table: "TreasuryAlerts",
                columns: new[] { "SourceEntityType", "SourceEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryAlerts_Status_Severity",
                table: "TreasuryAlerts",
                columns: new[] { "Status", "Severity" });

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Users_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Users_ActorUserId",
                table: "AuditLogs");

            migrationBuilder.DropTable(
                name: "TreasuryAlerts");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActorUserId_OccurredAtUtc",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_OccurredAtUtc",
                table: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "UserAgent",
                table: "AuditLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogs\" " +
                "ALTER COLUMN \"MetadataJson\" TYPE text " +
                "USING \"MetadataJson\"::text;");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "EntityReference",
                table: "AuditLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogs\" " +
                "ALTER COLUMN \"BeforeValuesJson\" TYPE text " +
                "USING \"BeforeValuesJson\"::text;");

            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogs\" " +
                "ALTER COLUMN \"AfterValuesJson\" TYPE text " +
                "USING \"AfterValuesJson\"::text;");

            migrationBuilder.AlterColumn<string>(
                name: "ActorRole",
                table: "AuditLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActorEmail",
                table: "AuditLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Users_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
