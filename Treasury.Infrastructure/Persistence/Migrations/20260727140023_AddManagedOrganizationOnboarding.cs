using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedOrganizationOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.CreateTable(
                name: "OrganizationApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedOrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TaxIdentificationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AdminFirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdminLastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdminEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ContactPhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ApplicationNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecisionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProvisionedOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProvisionedLegalEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProvisionedBusinessUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminInvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationApplications", x => x.Id);
                    table.CheckConstraint("CK_OrganizationApplications_BaseCurrency", "char_length(\"BaseCurrency\") = 3");
                    table.CheckConstraint("CK_OrganizationApplications_CountryCode", "char_length(\"CountryCode\") = 2");
                    table.CheckConstraint("CK_OrganizationApplications_DecisionState", "(\"Status\" IN ('Submitted','UnderReview') AND \"DecisionAtUtc\" IS NULL) OR (\"Status\" IN ('Approved','Rejected') AND \"DecisionAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_OrganizationApplications_ProvisioningState", "\"Status\" <> 'Approved' OR (\"ProvisionedOrganizationId\" IS NOT NULL AND \"ProvisionedLegalEntityId\" IS NOT NULL AND \"ProvisionedBusinessUnitId\" IS NOT NULL AND \"AdminInvitationId\" IS NOT NULL)");
                    table.CheckConstraint("CK_OrganizationApplications_Status", "\"Status\" IN ('Submitted','UnderReview','Approved','Rejected')");
                    table.ForeignKey(
                        name: "FK_OrganizationApplications_BusinessUnits_ProvisionedBusinessU~",
                        column: x => x.ProvisionedBusinessUnitId,
                        principalTable: "BusinessUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationApplications_LegalEntities_ProvisionedLegalEnti~",
                        column: x => x.ProvisionedLegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationApplications_Organizations_ProvisionedOrganizat~",
                        column: x => x.ProvisionedOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationApplications_UserInvitations_AdminInvitationId",
                        column: x => x.AdminInvitationId,
                        principalTable: "UserInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationApplications_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Organization','OrganizationApplication','LegalEntity','BusinessUnit','OrganizationMembership','UserInvitation','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','CreditFacilityDrawdown','CreditFacilityRepayment','CreditFacilityInterestAccrualSnapshot','System')");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationApplications_AdminInvitationId",
                table: "OrganizationApplications",
                column: "AdminInvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationApplications_NormalizedOrganizationName_AdminEm~",
                table: "OrganizationApplications",
                columns: new[] { "NormalizedOrganizationName", "AdminEmail" },
                unique: true,
                filter: "\"Status\" IN ('Submitted','UnderReview')");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationApplications_ProvisionedBusinessUnitId",
                table: "OrganizationApplications",
                column: "ProvisionedBusinessUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationApplications_ProvisionedLegalEntityId",
                table: "OrganizationApplications",
                column: "ProvisionedLegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationApplications_ProvisionedOrganizationId",
                table: "OrganizationApplications",
                column: "ProvisionedOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationApplications_ReviewedByUserId",
                table: "OrganizationApplications",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationApplications_Status_SubmittedAtUtc",
                table: "OrganizationApplications",
                columns: new[] { "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationApplications_SubmissionKey",
                table: "OrganizationApplications",
                column: "SubmissionKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_EntityType",
                table: "AuditLogs",
                sql: "\"EntityType\" IN ('User','Role','Organization','LegalEntity','BusinessUnit','OrganizationMembership','UserInvitation','Account','AccountType','TransferRequest','PaymentRequest','ReversalRequest','ApprovalPolicy','ApprovalDecision','TreasuryTransaction','BankStatementImport','BankStatementLine','CashFlowForecastItem','FxRate','TreasuryAlert','InvestmentPlacement','InvestmentRolloverRequest','Counterparty','InvestmentLimit','CreditFacility','CreditFacilityDrawdown','CreditFacilityRepayment','CreditFacilityInterestAccrualSnapshot','System')");
        }
    }
}
