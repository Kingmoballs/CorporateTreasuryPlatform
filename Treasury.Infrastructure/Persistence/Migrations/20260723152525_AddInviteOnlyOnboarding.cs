using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInviteOnlyOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            /*
             * Existing accounts predate email verification.
             * Preserve their access while all accounts created
             * by the new flow are verified through possession
             * of an invitation token.
             */
            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "EmailVerifiedAtUtc" =
                    COALESCE("CreatedAt", NOW())
                WHERE "EmailVerifiedAtUtc" IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "UserInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInvitations", x => x.Id);
                    table.CheckConstraint("CK_UserInvitations_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_UserInvitations_FinalState", "NOT (\"AcceptedAtUtc\" IS NOT NULL AND \"RevokedAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_UserInvitations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserInvitations_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserInvitations_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Organization','LegalEntity','BusinessUnit','OrganizationMembership','UserInvitation','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','CreditFacilityDrawdown','CreditFacilityRepayment','CreditFacilityInterestAccrualSnapshot','System')");

            migrationBuilder.CreateIndex(
                name: "IX_UserInvitations_InvitedByUserId",
                table: "UserInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInvitations_OrganizationId_Email",
                table: "UserInvitations",
                columns: new[] { "OrganizationId", "Email" },
                unique: true,
                filter: "\"AcceptedAtUtc\" IS NULL AND \"RevokedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserInvitations_RoleId",
                table: "UserInvitations",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInvitations_TokenHash",
                table: "UserInvitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserInvitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAtUtc",
                table: "Users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Organization','LegalEntity','BusinessUnit','OrganizationMembership','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','CreditFacilityDrawdown','CreditFacilityRepayment','CreditFacilityInterestAccrualSnapshot','System')");
        }
    }
}
